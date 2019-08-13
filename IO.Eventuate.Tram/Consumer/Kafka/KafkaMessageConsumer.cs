/*
 * Ported from:
 * repo:	https://github.com/eventuate-tram/eventuate-tram-core
 * module:	eventuate-tram-consumer-kafka
 * package:	io.eventuate.tram.consumer.kafka
 */

using System;
using System.Collections.Generic;
using Confluent.Kafka;
using IO.Eventuate.Tram.Consumer.Common;
using IO.Eventuate.Tram.Local.Kafka.Consumer;
using IO.Eventuate.Tram.Messaging.Common;
using IO.Eventuate.Tram.Messaging.Consumer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IO.Eventuate.Tram.Consumer.Kafka
{
	public class KafkaMessageConsumer : IMessageConsumer, IDisposable
	{
		private readonly ILogger _logger;
		private readonly EventuateKafkaConsumerConfigurationProperties _eventuateKafkaConsumerConfigurationProperties;
		private readonly DecoratedMessageHandlerFactory _decoratedMessageHandlerFactory;
		private readonly ILoggerFactory _loggerFactory;
		private readonly IServiceScopeFactory _serviceScopeFactory;

		private readonly string _id = Guid.NewGuid().ToString();
		private readonly string _bootstrapServers;
		private readonly List<EventuateKafkaConsumer> _consumers = new List<EventuateKafkaConsumer>();

		public KafkaMessageConsumer(string bootstrapServers,
			EventuateKafkaConsumerConfigurationProperties eventuateKafkaConsumerConfigurationProperties,
			DecoratedMessageHandlerFactory decoratedMessageHandlerFactory, ILoggerFactory loggerFactory,
			IServiceScopeFactory serviceScopeFactory)
		{
			_bootstrapServers = bootstrapServers;
			_eventuateKafkaConsumerConfigurationProperties = eventuateKafkaConsumerConfigurationProperties;
			_decoratedMessageHandlerFactory = decoratedMessageHandlerFactory;
			_loggerFactory = loggerFactory;
			_serviceScopeFactory = serviceScopeFactory;
			_logger = _loggerFactory.CreateLogger<KafkaMessageConsumer>();
		}

		public IMessageSubscription Subscribe(string subscriberId, ISet<string> channels, MessageHandler handler)
		{
			var logContext = $"{nameof(Subscribe)} for subscriberId='{subscriberId}', " +
			                 $"channels='{String.Join(",", channels)}', " +
			                 $"handler='{handler.Method.Name}'";
			_logger.LogDebug($"+{logContext}");
			
			Action<SubscriberIdAndMessage, IServiceProvider> decoratedHandler = _decoratedMessageHandlerFactory.Decorate(handler);
			
			var swimLaneBasedDispatcher = new SwimlaneBasedDispatcher(subscriberId, _loggerFactory);

			EventuateKafkaConsumerMessageHandler kcHandler =
				(record, completionCallback) => swimLaneBasedDispatcher.Dispatch(ToMessage(record), record.Partition,
					message => Handle(message, completionCallback, subscriberId, decoratedHandler));
			
			var kc = new EventuateKafkaConsumer(subscriberId,
				kcHandler,
				new List<string>(channels),
				_bootstrapServers,
				_eventuateKafkaConsumerConfigurationProperties,
				_loggerFactory);

			_consumers.Add(kc);

			kc.Start();
			
			_logger.LogDebug($"-{logContext}");
			return new MessageSubscription(() =>
			{
				kc.Dispose();
				_consumers.Remove(kc);
			});
		}

		public void Handle(IMessage message, Action<Exception> completionCallback, string subscriberId,
			Action<SubscriberIdAndMessage, IServiceProvider> decoratedHandler)
		{
			try
			{
				// Creating a service scope and passing the scope's service provider to handlers
				// so they can resolve scoped services
				using (IServiceScope scope = _serviceScopeFactory.CreateScope())
				{
					decoratedHandler(new SubscriberIdAndMessage(subscriberId, message), scope.ServiceProvider);
					completionCallback(null);
				}
			}
			catch (Exception e)
			{
				completionCallback(e);
				throw;
			}
		}

		public string GetId()
		{
			return _id;
		}

		public void Close()
		{
			_logger.LogDebug($"+{nameof(Close)}");
			foreach (EventuateKafkaConsumer consumer in _consumers)
			{
				consumer.Dispose();
			}
			_consumers.Clear();
			_logger.LogDebug($"-{nameof(Close)}");
		}

		/// <inheritdoc />
		private class MessageSubscription : IMessageSubscription
		{
			private readonly Action _unsubscribe;

			public MessageSubscription(Action unsubscribe)
			{
				_unsubscribe = unsubscribe;
			}

			/// <inheritdoc />
			public void Unsubscribe()
			{
				_unsubscribe();
			}
		}
		
		private IMessage ToMessage(ConsumeResult<string, string> record)
		{
			return JsonMapper.FromJson<Message>(record.Value);
		}

		public void Dispose()
		{
			_logger.LogDebug($"+{nameof(Dispose)}");
			Close();
			_logger.LogDebug($"-{nameof(Dispose)}");
		}
	}
}
/*
 * Ported from:
 * repo:	https://github.com/eventuate-tram/eventuate-tram-core
 * module:	eventuate-tram-consumer-common
 * package:	io.eventuate.tram.consumer.common
 */

using System;
using System.Collections.Generic;

namespace IO.Eventuate.Tram.Consumer.Common
{
	public class MessageHandlerDecoratorChainBuilder
	{
		private readonly LinkedList<IMessageHandlerDecorator> _handlers = new LinkedList<IMessageHandlerDecorator>();

		public static MessageHandlerDecoratorChainBuilder StartingWith(IMessageHandlerDecorator smh)
		{
			var b = new MessageHandlerDecoratorChainBuilder();
			b.Add(smh);
			return b;
		}

		private void Add(IMessageHandlerDecorator smh)
		{
			_handlers.AddLast(smh);
		}

		public MessageHandlerDecoratorChainBuilder AndThen(IMessageHandlerDecorator smh)
		{
			Add(smh);
			return this;
		}

		public IMessageHandlerDecoratorChain AndFinally(Action<SubscriberIdAndMessage, IServiceProvider> consumer)
		{
			return BuildChain(_handlers.First, consumer);
		}

		private static IMessageHandlerDecoratorChain BuildChain(LinkedListNode<IMessageHandlerDecorator> handlersHead,
			Action<SubscriberIdAndMessage, IServiceProvider> consumer)
		{
			if (handlersHead == null)
			{
				return new MessageHandlerDecoratorChain(consumer);
			}
			else
			{
				LinkedListNode<IMessageHandlerDecorator> tail = handlersHead.Next;
				return new MessageHandlerDecoratorChain((subscriberIdAndMessage, serviceProvider) =>
					handlersHead.Value.Accept(subscriberIdAndMessage, serviceProvider, BuildChain(tail, consumer)));
			}
		}

		private class MessageHandlerDecoratorChain : IMessageHandlerDecoratorChain
		{
			private readonly Action<SubscriberIdAndMessage, IServiceProvider> _action;

			public MessageHandlerDecoratorChain(Action<SubscriberIdAndMessage, IServiceProvider> action)
			{
				_action = action;
			}
			
			public void InvokeNext(SubscriberIdAndMessage subscriberIdAndMessage, IServiceProvider serviceProvider)
			{
				_action(subscriberIdAndMessage, serviceProvider);
			}
		}
	}
}
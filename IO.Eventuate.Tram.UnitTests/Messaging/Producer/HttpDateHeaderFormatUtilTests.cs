﻿using System;
using System.Collections.Generic;
using System.Text;
using IO.Eventuate.Tram.Messaging.Producer;
using NUnit.Framework;

namespace IO.Eventuate.Tram.UnitTests.Messaging.Producer
{
    public class HttpDateHeaderFormatUtilTests
    {
        [Test]
        public void NowAsHttpDateString_GetResult_NotNull()
        {
            Assert.NotNull((HttpDateHeaderFormatUtil.NowAsHttpDateString()));
        }
    }
}

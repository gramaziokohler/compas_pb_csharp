using Xunit;
using CompasPb.Data;
using System;
using System.Linq;
using System.Collections.Generic;
using Google.Protobuf;

public class DataHandlerTest
{
  [Fact]
  public void TestHandler()
  {
    Assert.NotNull(new DataHandler());
  }
}
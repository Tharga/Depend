using Tharga.Depend.Services;
using Xunit;

namespace Tharga.Depend.Tests;

public class OutputServiceTests
{
    [Fact]
    public void GetHelp()
    {
        //Arrange
        var sut = new OutputService();

        //Act
        sut.PrintHelp();

        //Asert
    }
}
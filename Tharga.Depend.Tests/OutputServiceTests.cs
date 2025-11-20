using Moq.AutoMock;
using Tharga.Depend.Features.Output;
using Xunit;

namespace Tharga.Depend.Tests;

public class OutputServiceTests
{
    [Fact]
    public void GetHelp()
    {
        //Arrange
        var mocker = new AutoMocker();
        var sut = mocker.CreateInstance<OutputService>();

        //Act
        sut.PrintHelp();

        //Asert
    }
}
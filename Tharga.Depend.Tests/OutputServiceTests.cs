using Moq;
using Moq.AutoMock;
using Tharga.Depend.Features.Output;
using Xunit;

namespace Tharga.Depend.Tests;

public class OutputServiceTests
{
    [Fact(Skip = "Cannot find file.")]
    public void GetHelp()
    {
        //Arrange
        var mocker = new AutoMocker(MockBehavior.Strict);

        var fileServiceMock = new Mock<IFileService>(MockBehavior.Strict);
        fileServiceMock.Setup(x => x.ReadAllText(It.IsAny<string>())).Returns("Content");

        mocker.Use(fileServiceMock);
        var sut = mocker.CreateInstance<OutputService>();

        //Act
        sut.PrintHelp();

        //Asert
        fileServiceMock.Verify(x => x.ReadAllText(It.IsAny<string>()), Times.Once);
    }
}
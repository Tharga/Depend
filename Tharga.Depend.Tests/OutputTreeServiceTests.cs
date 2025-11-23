using System.Text;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using Tharga.Depend.Features.Output;
using Xunit;

namespace Tharga.Depend.Tests;

public class OutputTreeServiceTests
{
    [Theory(Skip = "Fix")]
    [MemberData(nameof(RepoOutputData))]
    public void RepoOutputWithNoProjects(ViewMode viewMode, int repoCount)
    {
        //Arrange
        var repos = SampleBuilder.BuildRepos(repoCount, 0, 0).ToArray();
        var sb = new StringBuilder();
        var mocker = new AutoMocker(MockBehavior.Strict);
        var fakeOutputService = new Mock<IOutputService>(MockBehavior.Strict);
        fakeOutputService.Setup(x => x.WriteLine(It.IsAny<string>(), It.IsAny<ConsoleColor>())).Callback((string m, ConsoleColor _) => { sb.AppendLine(m); });
        mocker.Use(fakeOutputService);
        var sut = mocker.CreateInstance<OutputTreeService>();

        //Act
        sut.PrintTree(repos, viewMode, null, false);

        //Assert
        var lines = sb.ToString().Split("\r\n").TakeAllButLast().ToArray();
        switch (viewMode)
        {
            case ViewMode.Default:
            case ViewMode.Full:
            case ViewMode.RepoOnly:
                lines.Length.Should().Be(repoCount + 1);
                lines[0].Should().Be(".");
                if (repoCount > 0) lines.Last().Should().Be($"└── {repos.OrderBy(x => x.Name).Last().Name}");
                if (repoCount > 1) lines[1].Should().Be($"├── {repos.OrderBy(x => x.Name).First().Name}");
                break;
            case ViewMode.ProjectOnly:
                lines.Length.Should().Be(1);
                lines[0].Should().Be(".");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(viewMode), viewMode, null);
        }
    }

    [Theory(Skip = "Fix")]
    [MemberData(nameof(RepoOutputData))]
    public void RepoOutputWithOneProject(ViewMode viewMode, int repoCount)
    {
        //Arrange
        var repos = SampleBuilder.BuildRepos(repoCount, 1, 0).ToArray();
        var sb = new StringBuilder();
        var mocker = new AutoMocker(MockBehavior.Strict);
        var fakeOutputService = new Mock<IOutputService>(MockBehavior.Strict);
        fakeOutputService.Setup(x => x.WriteLine(It.IsAny<string>(), It.IsAny<ConsoleColor>())).Callback((string m, ConsoleColor _) => { sb.AppendLine(m); });
        mocker.Use(fakeOutputService);
        var sut = mocker.CreateInstance<OutputTreeService>();

        //Act
        sut.PrintTree(repos, viewMode, null, false);

        //Assert
        var lines = sb.ToString().Split("\r\n").TakeAllButLast().ToArray();
        switch (viewMode)
        {
            case ViewMode.Full:
            case ViewMode.Default:
            {
                lines.Length.Should().Be(repoCount * 2 + 1);
                lines[0].Should().Be(".");
                if (repoCount > 0) lines.Last().Should().Be($"    └── {repos.OrderBy(x => x.Name).Last().Projects.OrderBy(x => x.Name).Last().Name}");
                if (repoCount > 1) lines[1].Should().Be($"├── {repos.OrderBy(x => x.Name).First().Name}");
                if (repoCount > 1) lines[2].Should().Be($"│   └── {repos.OrderBy(x => x.Name).First().Projects.OrderBy(x => x.Name).First().Name}");
                break;
            }
            //case ViewMode.ProjectOnly:
            //    throw new NotImplementedException();
            case ViewMode.RepoOnly:
            {
                lines.Length.Should().Be(repoCount + 1);
                lines[0].Should().Be(".");
                if (repoCount > 0) lines.Last().Should().Be($"└── {repos.OrderBy(x => x.Name).Last().Name}");
                if (repoCount > 1) lines[1].Should().Be($"├── {repos.OrderBy(x => x.Name).First().Name}");
                break;
            }
            //default:
            //    throw new ArgumentOutOfRangeException(nameof(viewMode), viewMode, null);
        }
    }

    [Theory(Skip = "Fix")]
    [MemberData(nameof(RepoOutputData))]
    public void RepoOutputWithOneProjectAndOnePckageLevel(ViewMode viewMode, int repoCount)
    {
        //Arrange
        var repos = SampleBuilder.BuildRepos(repoCount, 1, 1).ToArray();
        var sb = new StringBuilder();
        var mocker = new AutoMocker(MockBehavior.Strict);
        var fakeOutputService = new Mock<IOutputService>(MockBehavior.Strict);
        fakeOutputService.Setup(x => x.WriteLine(It.IsAny<string>(), It.IsAny<ConsoleColor>())).Callback((string m, ConsoleColor _) => { sb.AppendLine(m); });
        mocker.Use(fakeOutputService);
        var sut = mocker.CreateInstance<OutputTreeService>();

        //Act
        sut.PrintTree(repos, viewMode, null, false);

        //Assert
        var lines = sb.ToString().Split("\r\n").TakeAllButLast().ToArray();
        switch (viewMode)
        {
            case ViewMode.Full:
            {
                lines.Length.Should().Be(repoCount * 3 + 1);
                lines[0].Should().Be(".");
                if (repoCount > 0)
                {
                    var packageInfo = repos.OrderBy(x => x.Name).Last().Projects.OrderBy(x => x.Name).Last().Packages.OrderBy(x => x.Name).Last();
                    lines.Last().Should().Be($"        └── {packageInfo.Name} ({packageInfo.Version})");
                }

                if (repoCount > 1) lines[1].Should().Be($"├── {repos.OrderBy(x => x.Name).First().Name}");
                if (repoCount > 1) lines[2].Should().Be($"│   └── {repos.OrderBy(x => x.Name).First().Projects.OrderBy(x => x.Name).First().Name}");
                break;
            }
            case ViewMode.Default:
            {
                lines.Length.Should().Be(repoCount * 2 + 1);
                lines[0].Should().Be(".");
                if (repoCount > 0) lines.Last().Should().Be($"    └── {repos.OrderBy(x => x.Name).Last().Projects.OrderBy(x => x.Name).Last().Name}");
                if (repoCount > 1) lines[1].Should().Be($"├── {repos.OrderBy(x => x.Name).First().Name}");
                if (repoCount > 1) lines[2].Should().Be($"│   └── {repos.OrderBy(x => x.Name).First().Projects.OrderBy(x => x.Name).First().Name}");
                break;
            }
            case ViewMode.RepoOnly:
            {
                lines.Length.Should().Be(repoCount + 1);
                lines[0].Should().Be(".");
                if (repoCount > 0) lines.Last().Should().Be($"└── {repos.OrderBy(x => x.Name).Last().Name}");
                if (repoCount > 1) lines[1].Should().Be($"├── {repos.OrderBy(x => x.Name).First().Name}");
                break;
            }
            //case ViewMode.ProjectOnly:
            //    throw new NotImplementedException();
            //    break;
            //default:
            //    throw new ArgumentOutOfRangeException(nameof(viewMode), viewMode, null);
        }
    }

    public static IEnumerable<object[]> RepoOutputData()
    {
        var modes = Enum.GetValues<ViewMode>();

        foreach (var mode in modes)
        {
            for (var i = 0; i <= 3; i++)
            {
                yield return [mode, i];
            }
        }
    }
}
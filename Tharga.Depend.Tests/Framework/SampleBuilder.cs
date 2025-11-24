using Bogus;
using Tharga.Depend.Features.Project;
using Tharga.Depend.Features.Repo;

namespace Tharga.Depend.Tests.Framework;

internal static class SampleBuilder
{
    public static IEnumerable<GitRepositoryInfo> BuildRepos(int repoCount = 1, int projectCount = 1, int packageCount = 1)
    {
        var faker = new Faker();

        for (var i = 0; i < repoCount; i++)
        {
            yield return new GitRepositoryInfo
            {
                Name = faker.Company.CompanyName(),
                Path = faker.System.DirectoryPath(),
                Projects = BuildProjects(projectCount, packageCount).ToArray()
            };
        }
    }

    private static IEnumerable<ProjectInfo> BuildProjects(int projectCount, int packageCount)
    {
        var faker = new Faker();

        for (var i = 0; i < projectCount; i++)
        {
            yield return new ProjectInfo
            {
                Name = faker.Company.CompanyName(),
                Path = faker.System.DirectoryPath(),
                TargetFramework = null,
                PackageId = null,
                Packages = BuildPackages(packageCount).ToArray()
            };
        }
    }

    private static IEnumerable<PackageInfo> BuildPackages(int packageCount)
    {
        var faker = new Faker();

        for (var i = 0; i < packageCount; i++)
        {
            yield return new PackageInfo
            {
                Name = faker.Company.CompanyName(),
                Path = faker.System.DirectoryPath(),
                Type = PackageType.Project,
                PackageId = null,
                Version = "1.2.3.4"
            };
        }
    }
}
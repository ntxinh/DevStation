using DevStation.Application.Modules.Projects.CreateProject;

namespace DevStation.Tests.Unit.Projects;

public class CreateProjectValidatorTests
{
    private static CreateProjectRequest Valid() => new()
    {
        Name = "demo",
        RepositoryUrl = "https://github.com/x/y",
        LocalPath = "/home/u/repo",
        DefaultBranch = "main",
        Technology = "dotnet",
        Tags = ["a"],
    };

    [Fact]
    public void Valid_request_has_no_errors()
        => Assert.Empty(CreateProjectValidator.Validate(Valid()));

    [Theory]
    [InlineData(null)][InlineData("")][InlineData("   ")]
    public void Name_required(string? name)
    {
        var r = Valid() with { Name = name! };
        Assert.NotEmpty(CreateProjectValidator.Validate(r));
    }

    [Fact]
    public void Name_over_200_fails()
    {
        var r = Valid() with { Name = new string('x', 201) };
        Assert.NotEmpty(CreateProjectValidator.Validate(r));
    }

    [Fact]
    public void Name_at_200_passes()
        => Assert.Empty(CreateProjectValidator.Validate(Valid() with { Name = new string('x', 200) }));

    [Theory]
    [InlineData("ftp://x")][InlineData("not a url")][InlineData("javascript:alert(1)")]
    public void RepositoryUrl_must_be_http_or_https(string url)
        => Assert.NotEmpty(CreateProjectValidator.Validate(Valid() with { RepositoryUrl = url }));

    [Theory]
    [InlineData(null)][InlineData("https://github.com/x/y")][InlineData("http://git.local/r")]
    public void RepositoryUrl_http_or_empty_ok(string? url)
        => Assert.Empty(CreateProjectValidator.Validate(Valid() with { RepositoryUrl = url }));

    [Theory]
    [InlineData("relative/path")][InlineData("./repo")][InlineData("repo")]
    public void LocalPath_must_be_absolute(string path)
        => Assert.NotEmpty(CreateProjectValidator.Validate(Valid() with { LocalPath = path }));

    [Fact]
    public void LocalPath_absolute_or_empty_ok()
    {
        Assert.Empty(CreateProjectValidator.Validate(Valid() with { LocalPath = "/home/u/repo" }));
        Assert.Empty(CreateProjectValidator.Validate(Valid() with { LocalPath = null }));
    }
}

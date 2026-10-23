using InvoiceApp.Domain;
using Xunit;

namespace InvoiceApp.Tests;

public class ArchitectureTests
{
    [Fact]
    public void Domain_DoesNotReferenceApi()
    {
        var referencedAssemblies = typeof(Invoice).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(
            referencedAssemblies,
            assembly => assembly.Name == "InvoiceApp.Api");
    }
}

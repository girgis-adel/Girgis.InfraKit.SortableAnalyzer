using Microsoft;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace Girgis.InfraKit.SortableAnalyzer.Test
{
    [TestClass]
    public class GirgisInfraKitSortableAnalyzerUnitTest
    {
        private static async Task VerifyFixAsync(string source, string fixedSource, params DiagnosticResult[] expectedDiagnostics)
        {
            var test = new CSharpCodeFixTest<
                GirgisInfraKitSortableAnalyzerAnalyzer,
                GirgisInfraKitSortableAnalyzerCodeFixProvider,
                MSTestVerifier>
            {
                TestCode = source,
                FixedCode = fixedSource,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };

            test.ExpectedDiagnostics.AddRange(expectedDiagnostics);
            await test.RunAsync();
        }

        [TestMethod]
        public async Task AddsSortable_WhenMissingOnDefault()
        {
            var source = @"
        using System;

        [AttributeUsage(AttributeTargets.Class)]
        public class SortableDefaultAttribute : Attribute
        {
            public SortableDefaultAttribute(string name) { }
        }

        [AttributeUsage(AttributeTargets.Property)]
        public class SortableAttribute : Attribute { }

        [SortableDefault(""Id"")]
        public class User
        {
            public int Id { get; set; }
        }
        ";

            var fixedSource = @"
        using System;

        [AttributeUsage(AttributeTargets.Class)]
        public class SortableDefaultAttribute : Attribute
        {
            public SortableDefaultAttribute(string name) { }
        }

        [AttributeUsage(AttributeTargets.Property)]
        public class SortableAttribute : Attribute { }

        [SortableDefault(""Id"")]
        public class User
        {
            [Sortable]
            public int Id { get; set; }
        }
        ";

            var expectedDiagnostics = new[]
            {
        VerifyCS.Diagnostic(SortablePropertyAnalyzer.SORT002).WithSpan(13, 14, 13, 18).WithArguments("Id"),
        VerifyCS.Diagnostic(SortablePropertyAnalyzer.SORT003).WithSpan(13, 14, 13, 18).WithArguments("User"),
    };

            await VerifyFixAsync(source, fixedSource, expectedDiagnostics);
        }

    }
}

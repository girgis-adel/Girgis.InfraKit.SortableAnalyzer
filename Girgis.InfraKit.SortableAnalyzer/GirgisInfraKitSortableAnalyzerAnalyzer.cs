using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Girgis.InfraKit.SortableAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class GirgisInfraKitSortableAnalyzerAnalyzer : DiagnosticAnalyzer
    {
        private static readonly HashSet<string> SupportedSortableTypes = new HashSet<string>()
        {
            "System.String",
            "System.Int16",
            "System.Int32",
            "System.Int64",
            "System.UInt16",
            "System.UInt32",
            "System.UInt64",
            "System.Single",
            "System.Double",
            "System.Decimal",
            "System.DateTime",
            "System.DateTimeOffset"
        };

        private static readonly DiagnosticDescriptor MissingSortableDefault = new DiagnosticDescriptor(
            "SORT001",
            "Missing [SortableDefault]",
            "Class '{0}' has [Sortable] properties but no [SortableDefault] defined.",
            "Usage",
            DiagnosticSeverity.Error, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor InvalidDefaultReference = new DiagnosticDescriptor(
            "SORT002",
            "[SortableDefault] must refer to a property with [Sortable]",
            "Property '{0}' must be marked with [Sortable] to be used in [SortableDefault].",
            "Usage",
            DiagnosticSeverity.Error, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor UnusedDefaultAttribute = new DiagnosticDescriptor(
            "SORT003",
            "[SortableDefault] used without any [Sortable] property",
            "Class '{0}' uses [SortableDefault] but has no [Sortable] properties.",
            "Usage",
            DiagnosticSeverity.Error, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor InvalidSortableType = new DiagnosticDescriptor(
            "SORT004",
            "[Sortable] applied to unsupported type",
            "Property '{0}' has unsupported type '{1}' for sorting.",
            "Usage",
            DiagnosticSeverity.Error, isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get
            {
                return ImmutableArray.Create(
                    MissingSortableDefault,
                    InvalidDefaultReference,
                    UnusedDefaultAttribute,
                    InvalidSortableType
                    );
            }
        }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(AnalyzeClass, SymbolKind.NamedType);
        }

        private static void AnalyzeClass(SymbolAnalysisContext context)
        {
            var classSymbol = (INamedTypeSymbol)context.Symbol;

            var sortableProps = GetAllSortableProperties(classSymbol);
            var defaultAttr = GetOwnOrInheritedSortableDefault(classSymbol, out var definingType);

            // Rules
            // 
            // Rule 1:  Class with any [Sortable] properties must have [SortableDefault] on it,
            //          referring to one of those properties.
            // 
            // Rule 2:  Child class can override parent [SortableDefault] if it defines a different [Sortable] property,
            //          or inherit it.
            //
            // Rule 3: 	[SortableDefault] is invalid if no property (in current or base class) has [Sortable].
            //
            // Rule 4:  If any [Sortable] is present, [SortableDefault] must exist on the class or ancestor.
            //
            // Rule 5:  [Sortable] can only be applied on EF Core
            //          orderable data types (e.g. int, long, DateTime, string, etc.).
            //


            // Rule 3: SortableDefault used but no sortable props
            if (defaultAttr != null && sortableProps.Count == 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(UnusedDefaultAttribute, classSymbol.Locations[0], classSymbol.Name));
            }

            // Rule 1 & 4: Sortable props but no default
            if (sortableProps.Count > 0 && defaultAttr == null)
            {
                context.ReportDiagnostic(Diagnostic.Create(MissingSortableDefault, classSymbol.Locations[0], classSymbol.Name));
            }

            // Rule 2: Ensure default references a sortable property
            if (defaultAttr != null)
            {
                var defaultPropName = defaultAttr.ConstructorArguments[0].Value as string;
                var defaultProp = classSymbol.GetMembers().OfType<IPropertySymbol>().FirstOrDefault(p => p.Name == defaultPropName)
                                  ?? definingType?.GetMembers().OfType<IPropertySymbol>().FirstOrDefault(p => p.Name == defaultPropName);

                if (defaultProp == null || !HasSortableAttribute(defaultProp))
                {
                    context.ReportDiagnostic(Diagnostic.Create(InvalidDefaultReference, classSymbol.Locations[0], defaultPropName));
                }
            }

            // Rule 5: Check type validity for all [Sortable]
            foreach (var prop in sortableProps)
            {
                var typeName = prop.Type.ToDisplayString();
                if (!SupportedSortableTypes.Contains(typeName))
                {
                    context.ReportDiagnostic(Diagnostic.Create(InvalidSortableType, prop.Locations[0], prop.Name, typeName));
                }
            }
        }

        private static List<IPropertySymbol> GetAllSortableProperties(INamedTypeSymbol classSymbol)
        {
            var props = new List<IPropertySymbol>();

            for (var type = classSymbol; type != null; type = type.BaseType)
            {
                props.AddRange(
                    type.GetMembers().OfType<IPropertySymbol>()
                        .Where(HasSortableAttribute)
                );
            }

            return props;
        }

        private static bool HasSortableAttribute(IPropertySymbol prop)
        {
            return prop.GetAttributes().Any(a => a.AttributeClass?.Name == "SortableAttribute");
        }

        private static AttributeData GetOwnOrInheritedSortableDefault(INamedTypeSymbol type, out INamedTypeSymbol definingType)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                var attr = current.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "SortableDefaultAttribute");
                if (attr != null)
                {
                    definingType = current;
                    return attr;
                }
            }

            definingType = null;
            return null;
        }
    }
}

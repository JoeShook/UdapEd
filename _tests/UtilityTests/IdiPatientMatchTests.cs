using Firely.Fhir.Packages;
using Firely.Fhir.Validation;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Specification;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.Specification.Terminology;
using NSubstitute;
using Xunit.Abstractions;


namespace UtilityTests;
public class IdiPatientMatchTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public IdiPatientMatchTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }


    [Fact]
    public void ValidateInParameters()
    {
        // var packageSource = FhirPackageSource.CreateCorePackageSource(ModelInfo.ModelInspector, FhirRelease.R4B, "https://packages.simplifier.net");
        var packageSource = new DirectorySource(@"C:\Users\Joseph.Shook\.fhir\packages\hl7.fhir.r4b.core#4.3.0", new DirectorySourceSettings { IncludeSubDirectories = true });

        var coreSource = new CachedResolver(packageSource);

        // Load the FHIR package
        var idiSource = new CachedResolver(new DirectorySource("C:\\Users\\Joseph.Shook\\.fhir\\packages\\hl7.fhir.us.identity-matching#2.0.0-ballot",
            new DirectorySourceSettings { IncludeSubDirectories = true }));

        var source = new MultiResolver(idiSource, coreSource);
        var settings = new ValidationSettings { ConformanceResourceResolver = source };
        // Create a validator
        var validator = new Validator(source, Substitute.For<ICodeValidationTerminologyService>(), null, settings);

        // Load the Parameter resource to be validated
        var json = File.ReadAllText("idi-match-in-parameters.json");
        var parser = new FhirJsonParser();
        var parameterResource = parser.Parse<Parameters>(json);

        // Validate the resource
        var result = validator.Validate(parameterResource);

        // Output the validation results
        if (result.Success)
        {
            _testOutputHelper.WriteLine("Validation succeeded!");
        }
        else
        {
            _testOutputHelper.WriteLine("Validation failed:");
            foreach (var issue in result.Issue)
            {
                _testOutputHelper.WriteLine($"- {issue.Severity}: {issue.Details.Text}");
            }
        }


    }

}


using Amazon.Lambda.Annotations;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.XRay.Recorder.Handlers.AwsSdk;
using AWS.Lambda.Powertools.Logging;
using AWS.Lambda.Powertools.Metrics;
using AWS.Lambda.Powertools.Tracing;
using DocProcessing.Shared.Model;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]
[assembly: LambdaGlobalProperties(GenerateMain = true)]
namespace ProcessTextractQueryResults;

public class Function(ITextractService textractService, IDataService dataService)
{
    private readonly ITextractService _textractService = textractService;
    private readonly IDataService _dataService = dataService;

    static Function()
    {
        AWSSDKHandler.RegisterXRayForAllServices();
    }

    [Tracing]
    [Metrics]
    [Logging]
    [LambdaFunction]
    public async Task<IdMessage> FunctionHandler(IdMessage input, ILambdaContext context)
    {
        var processData = await _dataService.GetData<ProcessData>(input.Id).ConfigureAwait(false);

        // Get the step functions Result
        var textractModel = await _textractService.GetBlocksForAnalysis(processData.OutputBucket, processData.TextractOutputKey).ConfigureAwait(false);

        // Get the query Results
        foreach (var query in processData.Queries)
        {
            var results = DocumentAnalysisUtilities.GetDocumentQueryResults(textractModel, query.QueryId);
            query.Result.AddRange(results);
            query.IsValid = results.Any();
        }

        // Save the query results back to the database, and clear the task token
        processData.ClearTextractJobData();

        await _dataService.SaveData(processData).ConfigureAwait(false);
        return input;
    }

}
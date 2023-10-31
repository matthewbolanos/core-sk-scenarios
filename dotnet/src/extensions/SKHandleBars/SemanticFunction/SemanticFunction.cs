// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.AI;
using Microsoft.SemanticKernel.AI.TextCompletion;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Services;
using YamlDotNet.Serialization;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel.TemplateEngine;

namespace Microsoft.SemanticKernel.Handlebars;

public sealed class SemanticFunction : ISKFunction, IDisposable
{
    public string Name { get; }

    public string PluginName { get; }

    public string Description { get; }

    public static SemanticFunction GetFunctionFromYaml(
        string filepath,
        ILoggerFactory? loggerFactory = null,
        CancellationToken cancellationToken = default)
    {
        var yamlContent = File.ReadAllText(filepath);
        return GetFunctionFromYamlContent(yamlContent, loggerFactory, cancellationToken);
    }

    public static SemanticFunction GetFunctionFromYamlContent(
        string yamlContent,
        ILoggerFactory? loggerFactory = null,
        CancellationToken cancellationToken = default)
    {
        var deserializer = new DeserializerBuilder()
            .Build();

        var skFunction = deserializer.Deserialize<SemanticFunctionModel>(yamlContent);

        List<ParameterView> inputParameters = new List<ParameterView>();
        foreach(var inputParameter in skFunction.InputVariables)
        {
            Type parameterViewType;
            switch(inputParameter.Type)
            {
                case "string":
                    parameterViewType = typeof(string);
                    break;
                case "number":
                    parameterViewType = typeof(double);
                    break;
                case "boolean":
                    parameterViewType = typeof(bool);
                    break;
                default:
                    parameterViewType = typeof(object);
                    break;
            }

            inputParameters.Add(new ParameterView(
                inputParameter.Name,
                inputParameter.Description,
                inputParameter.DefaultValue,
                parameterViewType,
                inputParameter.IsRequired
            ));
        }

        var func = new SemanticFunction(
            functionName: skFunction.Name,
            template: skFunction.Template,
            templateFormat: skFunction.TemplateFormat,
            description: skFunction.Description,
            inputParameters: inputParameters,
            // outputParameter: skFunction.OutputVariable,
            loggerFactory: loggerFactory
        );

        return func;
    }

    public SemanticFunction(
        string functionName,
        string template,
        string templateFormat,
        string description,
        List<ParameterView> inputParameters,
        // SKVariableView outputParameter,
        ILoggerFactory? loggerFactory = null)
    {
        this._logger = loggerFactory is not null ? loggerFactory.CreateLogger(typeof(SemanticFunction)) : NullLogger.Instance;

        // Add logic to use the right template engine based on the template format
        this.PromptTemplate = template;
        this.PluginName = "";

        this.InputParameters = inputParameters;

        this.Name = functionName;
        this.Description = description;

    }

    public FunctionView Describe()
    {   
        return new FunctionView(
            this.Name,
            this.PluginName,
            this.Description,
            this.InputParameters
        );
    }

    public async Task<FunctionResult> InvokeAsync(
        IKernel kernel,
        SKContext executionContext,
        Dictionary<string, object?> variables,
        CancellationToken cancellationToken = default
    )
    {
        // TODO: make dynamic
        IAIService client = kernel.GetService<IAIService>("gpt-3.5-turbo");
        FunctionResult result;

        // Render the prompt
        string renderedPrompt = kernel.PromptTemplateEngine.Render(kernel, executionContext, PromptTemplate, variables, cancellationToken);

        if(client is IChatCompletion completion)
        {

            // Extract the chat history from the rendered prompt
            string pattern = @"<(user~|system~|assistant~)>(.*?)<\/\1>";
            MatchCollection matches = Regex.Matches(renderedPrompt, pattern, RegexOptions.Singleline);

            // Add the chat history to the chat
            ChatHistory chatMessages = completion.CreateNewChat();
            foreach (Match match in matches.Cast<Match>())
            {
                string role = match.Groups[1].Value;
                string message = match.Groups[2].Value;

                switch(role)
                {
                    case "user~":
                        chatMessages.AddUserMessage(message);
                        break;
                    case "system~":
                        chatMessages.AddSystemMessage(message);
                        break;
                    case "assistant~":
                        chatMessages.AddAssistantMessage(message);
                        break;
                }
            }
            
            // Get the completions
            IReadOnlyList<IChatResult> completionResults = await completion.GetChatCompletionsAsync(chatMessages, cancellationToken: cancellationToken).ConfigureAwait(false);
            var modelResults = completionResults.Select(c => c.ModelResult).ToArray();
            result = new FunctionResult(this.Name, this.PluginName, modelResults[0].GetOpenAIChatResult().Choice.Message.Content);
            result.Metadata.Add(AIFunctionResultExtensions.ModelResultsMetadataKey, modelResults);
        }
        else
        {
            throw new NotImplementedException();
        }

        return result;
    }

    private readonly ILogger _logger;
    private string PromptTemplate { get; }

    private IReadOnlyList<ParameterView> InputParameters { get; }

    public AIRequestSettings? RequestSettings => throw new NotImplementedException();

    public string SkillName => throw new NotImplementedException();

    public bool IsSemantic => throw new NotImplementedException();


    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public ISKFunction SetAIConfiguration(AIRequestSettings? requestSettings)
    {
        throw new NotImplementedException();
    }

    public ISKFunction SetAIService(Func<ITextCompletion> serviceFactory)
    {
        throw new NotImplementedException();
    }

    public ISKFunction SetDefaultFunctionCollection(IReadOnlyFunctionCollection functions)
    {
        throw new NotImplementedException();
    }

    public ISKFunction SetDefaultSkillCollection(IReadOnlyFunctionCollection skills)
    {
        throw new NotImplementedException();
    }

    Task<Orchestration.FunctionResult> ISKFunction.InvokeAsync(SKContext context, AIRequestSettings? requestSettings, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    SemanticKernel.FunctionView ISKFunction.Describe()
    {
        throw new NotImplementedException();
    }
}
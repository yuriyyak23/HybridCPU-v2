namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf07ExitExecuteFalseOwnerInventoryTests
{
    [Fact]
    public void ConcreteExecuteLiteralFalseOwners_AreOnlyTheFiveIndependentlyLedgeredMemoryContours()
    {
        string root = FindRepositoryRoot();
        IReadOnlyList<ExecuteBody> executeBodies = ReadExecuteBodies(root);

        string[] literalFalseOwners = executeBodies
            .Where(body => body.Text.Contains("return false", StringComparison.Ordinal))
            .Select(body => body.Owner)
            .OrderBy(owner => owner, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "LoadMicroOp",
                "LoadSegmentMicroOp",
                "StoreMicroOp",
                "StoreSegmentMicroOp",
                "VectorTransferMicroOp"
            },
            literalFalseOwners);

        Assert.Contains(executeBodies, body =>
            body.Owner == "LoadMicroOp" &&
            body.Text.Contains("TryAcceptSingleLaneScalarLoad", StringComparison.Ordinal) &&
            body.Text.Contains("TryTakeCompletion", StringComparison.Ordinal));
        Assert.Contains(executeBodies, body =>
            body.Owner == "StoreMicroOp" &&
            body.Text.Contains("TryAcceptSingleLaneScalarStore", StringComparison.Ordinal) &&
            body.Text.Contains("TryTakeCompletion", StringComparison.Ordinal));
        Assert.Contains(executeBodies, body =>
            body.Owner == "LoadSegmentMicroOp" &&
            body.Text.Contains("TryAcceptVectorSegmentLoad", StringComparison.Ordinal) &&
            body.Text.Contains("TryTakeCompletion", StringComparison.Ordinal));
        Assert.Contains(executeBodies, body =>
            body.Owner == "StoreSegmentMicroOp" &&
            body.Text.Contains("ExecutionState.StoringResults", StringComparison.Ordinal));
        Assert.Contains(executeBodies, body =>
            body.Owner == "VectorTransferMicroOp" &&
            body.Text.Contains("TryAcceptCanonicalVectorTransfer", StringComparison.Ordinal) &&
            body.Text.Contains("TryTakeCompletion", StringComparison.Ordinal));
    }

    [Fact]
    public void ProductionExecuteCallers_AreSingleLaneExplicitGenericAndExplicitAssist()
    {
        string root = FindRepositoryRoot();
        string helpers = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/Materialization/CPU_Core.PipelineExecution.ExecuteHelpers.cs");

        Assert.Equal(1, CountOccurrences(helpers, "ExecuteMicroOpWithStableCoreIdentity(pipeEX.MicroOp)"));
        Assert.Equal(1, CountOccurrences(helpers, "ExecuteMicroOpWithStableCoreIdentity(lane.MicroOp)"));
        Assert.Equal(1, CountOccurrences(helpers, "ExecuteMicroOpWithStableCoreIdentity(lane.MicroOp!)"));

        string assist = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/Assist/Runtime/CPU_Core.Assist.cs");
        string assistExecution = Between(
            assist,
            "internal bool ExecuteAssistMicroOp",
            "private bool TryExecuteAssistMicroOpOnCarrier");
        Assert.DoesNotContain("return false", assistExecution, StringComparison.Ordinal);
        Assert.Contains("return true", assistExecution, StringComparison.Ordinal);
    }

    private static IReadOnlyList<ExecuteBody> ReadExecuteBodies(string root)
    {
        string directory = Path.Combine(root,
            "HybridCPU_ISE",
            "CloseToHSL",
            "Core",
            "Pipeline",
            "MicroOps");

        return Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .SelectMany(path => ExtractExecuteBodies(File.ReadAllText(path)))
            .ToArray();
    }

    private static IEnumerable<ExecuteBody> ExtractExecuteBodies(string source)
    {
        const string marker = "public override bool Execute(ref Processor.CPU_Core core)";
        int searchStart = 0;
        while (true)
        {
            int methodStart = source.IndexOf(marker, searchStart, StringComparison.Ordinal);
            if (methodStart < 0)
                yield break;

            int ownerStart = source.LastIndexOf("class ", methodStart, StringComparison.Ordinal);
            Assert.True(ownerStart >= 0, "Concrete Execute must be nested under a class declaration.");
            int ownerNameStart = ownerStart + "class ".Length;
            int ownerNameEnd = source.IndexOfAny(new[] { ' ', ':', '\r', '\n' }, ownerNameStart);
            Assert.True(ownerNameEnd > ownerNameStart, "Concrete Execute owner name was not readable.");
            string owner = source[ownerNameStart..ownerNameEnd];

            int bodyStart = methodStart + marker.Length;
            while (char.IsWhiteSpace(source[bodyStart]))
                bodyStart++;

            int bodyEnd;
            if (source.AsSpan(bodyStart).StartsWith("=>", StringComparison.Ordinal))
            {
                bodyEnd = source.IndexOf(';', bodyStart);
                Assert.True(bodyEnd > bodyStart, "Expression-bodied Execute must terminate.");
            }
            else
            {
                Assert.Equal('{', source[bodyStart]);
                int depth = 0;
                bodyEnd = bodyStart;
                for (; bodyEnd < source.Length; bodyEnd++)
                {
                    if (source[bodyEnd] == '{')
                        depth++;
                    else if (source[bodyEnd] == '}' && --depth == 0)
                        break;
                }
                Assert.True(bodyEnd < source.Length, "Block-bodied Execute must terminate.");
            }

            yield return new ExecuteBody(owner, source[methodStart..(bodyEnd + 1)]);
            searchStart = bodyEnd + 1;
        }
    }

    private static string Between(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, startMarker);
        int end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(end > start, endMarker);
        return source[start..end];
    }

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static string Read(string root, string relative) =>
        File.ReadAllText(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory); current is not null; current = current.Parent)
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")))
                return current.FullName;
        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private sealed record ExecuteBody(string Owner, string Text);
}

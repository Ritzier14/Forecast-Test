namespace ProjectCostForecast.App.Services;

public enum CloseDecision
{
    Save,
    Discard,
    Cancel
}

public static class CloseDecisionPolicy
{
    public static bool ShouldClose(bool isDirty, CloseDecision decision, Func<bool> save)
    {
        ArgumentNullException.ThrowIfNull(save);

        if (!isDirty)
        {
            return true;
        }

        return decision switch
        {
            CloseDecision.Save => TrySave(save),
            CloseDecision.Discard => true,
            CloseDecision.Cancel => false,
            _ => false
        };
    }

    private static bool TrySave(Func<bool> save)
    {
        try
        {
            return save();
        }
        catch
        {
            return false;
        }
    }
}

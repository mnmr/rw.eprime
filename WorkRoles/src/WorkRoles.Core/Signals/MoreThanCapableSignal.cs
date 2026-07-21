namespace WorkRoles.Core.Signals
{
    /// The normalized signal emitted for one work type reported as hated by
    /// More Than Capable. Game-specific API binding remains in the adapter.
    public static class MoreThanCapableSignal
    {
        public const string PackageId = "void.MoreThanCapable";
        public const string SourceDefName = "HatedWork";

        public static Signal Create(
            string workTypeDefName,
            string label,
            string description)
        {
            workTypeDefName = SignalCondition.Required(
                workTypeDefName, nameof(workTypeDefName));
            return new Signal(
                SignalType.Active,
                new SignalSource(
                    SignalSourceKind.WorkAversion,
                    SourceDefName,
                    PackageId),
                skillDefName: null,
                effects: new[]
                {
                    new SignalEffect(
                        SignalEffectKind.WorkPreference,
                        SignalOperation.Descriptive,
                        null,
                        SignalValueUnit.None,
                        workTypeDefName,
                        new[]
                        {
                            new SignalCondition(
                                "work-type:hated",
                                "The source mod reports this work type as hated."),
                        }),
                },
                new SignalUi(
                    label,
                    description,
                    null,
                    "Awful",
                    "Awful",
                    "More Than Capable"),
                workTypeDefName: workTypeDefName);
        }
    }
}

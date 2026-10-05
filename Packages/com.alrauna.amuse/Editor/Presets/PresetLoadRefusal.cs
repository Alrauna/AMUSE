namespace Alrauna.Amuse.Editor.Presets
{
    /// <summary>
    /// Why a preset file did not load. The value list is closed: a new
    /// cause means a new named value, never a reused one. None is the
    /// zero value, so a fresh field means "no problem".
    /// </summary>
    internal enum PresetLoadRefusal
    {
        None = 0,
        FileMissing,
        MalformedJson,
        UnknownSchemaVersion,
        MissingField,
        UnknownField,
        WrongValueType,
        ValueOutOfRange,
    }
}

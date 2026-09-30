internal readonly record struct WindowBookmark(nint Handle, string Title)
{
    public override string ToString() => Title;
}

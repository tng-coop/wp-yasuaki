namespace Editor.WordPress
{
    public sealed class WpMe
    {
        public long Id { get; set; }
        public string? Name { get; set; }
        public string? Username { get; set; } // some sites expose "slug"/"username"
    }
}

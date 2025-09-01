namespace BlazorWP.Data
{
    public class AppFlags
    {
        public bool Basic { get; private set; }

        public void SetBasic(bool value)
        {
            Basic = value;
        }
    }
}

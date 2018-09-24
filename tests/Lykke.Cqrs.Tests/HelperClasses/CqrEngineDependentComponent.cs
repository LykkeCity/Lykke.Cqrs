namespace Lykke.Cqrs.Tests
{
    class CqrEngineDependentComponent
    {
        public static bool Started { get; set; }
        public CqrEngineDependentComponent(ICqrsEngine engine)
        {
        }
        public void Start()
        {
            Started = true;
        }
    }
}
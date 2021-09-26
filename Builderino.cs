namespace BranchReload2
{
    public class Builderino
    {
        
        public ConsoleMagic ConsoleMagic { get; set; }
        
        public Builderino(AsinkOverseer overseer, ConsoleMagic consoleMagic)
        {
            ConsoleMagic = consoleMagic;;
            overseer.CheckYields();
        }


    }
}
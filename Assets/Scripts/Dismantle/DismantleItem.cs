using Rollrate.Data;

namespace Rollrate.Dismantle
{
    /// <summary>One row's worth of data: either a distinct owned die OR a distinct owned module, never both.</summary>
    public class DismantleItem
    {
        public DieData die;
        public ModuleData module;
        public bool IsModule => module != null;
    }
}

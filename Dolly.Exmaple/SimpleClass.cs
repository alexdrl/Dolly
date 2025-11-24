using Dolly.Exmaple.Dolly;

namespace Dolly.Exmaple;

[Clonable]
public partial class SimpleClass
{
    public string First { get; set; }
    public int Second { get; set; }
    [CloneIgnore]
    public float DontClone { get; set; }
}

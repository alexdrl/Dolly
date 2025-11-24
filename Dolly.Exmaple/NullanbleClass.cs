using Dolly.Exmaple.Dolly;

namespace Dolly.Exmaple;
[Clonable]
public partial class NullanbleClass
{
    public SimpleClass ReferenceTypeNotNull { get; set; }
    public SimpleClass? ReferenceTypeNull { get; set; }
    public List<SimpleClass> ReferenceTypeNotNullList { get; set; }
    public List<SimpleClass?> ReferenceTypeNullList { get; set; }
    public List<SimpleClass>? ReferenceTypeNotNullListNull { get; set; }
    public List<SimpleClass?>? ReferenceTypeNullListNotNull { get; set; }
    public SimpleStruct ValueTypeNotNull { get; set; }
    public SimpleStruct? ValueTypeNull { get; set; }
    public List<SimpleStruct> ValueTypeNotNullList { get; set; }
    public List<SimpleStruct?> ValueTypeNullList { get; set; }
    public List<SimpleStruct>? ValueTypeNotNullListNull { get; set; }
    public List<SimpleStruct?>? ValueTypeNullListNotNull { get; set; }
}

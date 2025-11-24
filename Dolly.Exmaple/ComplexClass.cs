using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dolly.Exmaple.Dolly;

namespace Dolly.Exmaple;
[Clonable]
public partial class ComplexClass
{
    public SimpleClass SimpleClass { get; set; }
    public SimpleClass[] Array { get; set; }
    public List<SimpleClass> List { get; set; }
    public IEnumerable<SimpleClass> IEnumerable { get; set; }
}

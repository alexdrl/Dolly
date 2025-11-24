using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dolly.Exmaple.Dolly;

namespace Dolly.Exmaple;

[Clonable]
public partial class BaseClass
{
    public Guid Id { get; set; }
}

[Clonable]
public partial class InheritanceClass : BaseClass
{
    public string Name { get; set; }
}

[Clonable]
public sealed partial class SealedClass : BaseClass
{
    public string Name { get; set; }
}
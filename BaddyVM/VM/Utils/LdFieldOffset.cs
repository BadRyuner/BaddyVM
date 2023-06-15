using AsmResolver.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaddyVM.VM.Utils;
internal class LdFieldOffset : MetadataMember
{
	public IFieldDescriptor f;

	public LdFieldOffset(IFieldDescriptor f) : base(0)
	{
		this.f = f;
	}

	public override int GetHashCode() => f.GetHashCode();
	public override bool Equals(object obj) => f.Equals(obj);
}

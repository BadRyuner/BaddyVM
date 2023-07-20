using AsmResolver.DotNet;

namespace BaddyVM.VM.Utils;
internal class SignatureMember : MetadataMember
{
	internal MetadataMember inner;

	public SignatureMember(MetadataMember mem) : base(mem.MetadataToken)
	{
		inner = mem;
	}

	public override int GetHashCode() => inner.GetHashCode();
	public override string ToString() => inner.ToString();
	public override bool Equals(object obj) => inner.Equals(obj);
}

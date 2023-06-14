using AsmResolver.DotNet;
using AsmResolver.PE.DotNet.Metadata.Tables;

namespace BaddyVM.VM.Utils;
internal class LdTokenMember : MetadataMember
{
	internal MetadataMember target;

	public LdTokenMember(MetadataMember mem) : base(mem.MetadataToken)
	{
		target = mem;
	}

	public override bool Equals(object obj)
	{
		if (obj is LdTokenMember ld)
			return ld.MetadataToken == target.MetadataToken;
		return false;
	}

	public override int GetHashCode() => target.MetadataToken.GetHashCode();
}

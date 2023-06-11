using AsmResolver.DotNet;
using AsmResolver.PE.DotNet.Metadata.Tables;

namespace BaddyVM.VM.Utils;
internal class LdTokenMember : MetadataMember
{
	public LdTokenMember(MetadataToken token) : base(token)
	{
	}

	public override bool Equals(object obj)
	{
		if (obj is LdTokenMember ld)
			return ld.MetadataToken == this.MetadataToken;
		return false;
	}

	public override int GetHashCode() => this.MetadataToken.GetHashCode();
}

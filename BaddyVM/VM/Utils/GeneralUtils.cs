using AsmResolver;
using AsmResolver.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaddyVM.VM.Utils;
internal static class GeneralUtils
{
	internal static byte unsignedbyte = 0b0001_0000;
	internal static byte floatbyte	  = 0b0010_0000;

	internal static VMTypes MathWith(VMTypes f, VMTypes t) => f > t ? f : t; // float > unsigned > signed && longer > shorter
	internal static bool IsUnsigned(this VMTypes t) => ((byte)t & unsignedbyte) == unsignedbyte;
	internal static bool IsFloat(this VMTypes t) => ((byte)t & floatbyte) == floatbyte;

	/*
	private static byte[] getLoadCodeHeaderCache;

	internal static byte[] GetLoadCodeHeader()
	{
		if (getLoadCodeHeaderCache != null) 
			return getLoadCodeHeaderCache;
		using(Reloaded.Assembler.Assembler asm = new())
		{
			getLoadCodeHeaderCache = asm.Assemble("use64\nmov rax, qword [CodeStart]\nret\nCodeStart: dq 0");
		}
		return getLoadCodeHeaderCache;
	}
	*/

	private static Utf8String DelegateName = "Delegate";
	private static Utf8String SystemNM = "System";

	internal static bool IsDelegate(this ITypeDefOrRef type)
	{
		if (type.IsTypeOfUtf8(SystemNM, DelegateName) || type.Resolve().BaseType?.IsDelegate() == true)
			return true;
		return false;
	}
}

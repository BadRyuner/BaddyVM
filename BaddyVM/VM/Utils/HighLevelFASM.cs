using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Iced.Intel;

namespace BaddyVM.VM.Utils;
internal static class HighLevelFASM
{
	internal static StringBuilder Byte(this StringBuilder s, byte b)
	{
		s.AppendLine($"db {(sbyte)b}");
		return s;
	}

	internal static StringBuilder Short(this StringBuilder s, short shorrt)
	{
		s.AppendLine($"dw {shorrt}");
		return s;
	}

	internal static StringBuilder Ushort(this StringBuilder s, ushort shorrt)
	{
		s.AppendLine($"dw {shorrt}");
		return s;
	}

	internal static StringBuilder Code(this StringBuilder s, VMContext ctx, VMCodes c)
	{
		s.AppendLine($"db {ctx.EncryptVMCode(c)}");
		return s;
	}

	internal static StringBuilder SwapStack(this StringBuilder s, VMContext ctx)
	{
		s.AppendLine($"db {ctx.EncryptVMCode(VMCodes.SwapStack)}");
		return s;
	}

	internal static StringBuilder PushInt(this StringBuilder s, VMContext ctx, int i) => s.Code(ctx, VMCodes.Push4).Int(i);

	internal static StringBuilder Int(this StringBuilder s, int i)
	{
		s.AppendLine($"dd {i}");
		return s;
	}

	internal static StringBuilder Long(this StringBuilder s, long i)
	{
		s.AppendLine($"dq {i}");
		return s;
	}

	internal static StringBuilder LabelOffset(this StringBuilder s, int dest)
	{
		s.AppendLine($"ILl_{unnamed}: dw (IL_{dest} - ILl_{unnamed} - 2)"); unnamed++;
		return s;
	}

	internal static StringBuilder LabelOffset(this StringBuilder s, string dest)
	{
		s.AppendLine($"ILl_{unnamed}: dw ({dest} - ILl_{unnamed} - 2)"); unnamed++;
		return s;
	}

	private static long unnamed = 0;
}

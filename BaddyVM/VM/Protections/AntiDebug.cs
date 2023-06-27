using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE;
using AsmResolver.PE.Code;
using AsmResolver.PE.DotNet;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.Imports;
using BaddyVM.VM.Utils;
using Iced.Intel;
using System.Linq;
using System.Reflection;
using static Iced.Intel.AssemblerRegisters;

namespace BaddyVM.VM.Protections;
internal static class AntiDebug
{
	internal static bool ForceDisable = false;

	private static IMethodDescriptor Exit;
	private static ushort ExitIdx;

	private static MethodDefinition PebCheck; // kills x64dbg without PEB_Hider
	private static ushort PebCheckIdx;

	private static IMethodDescriptor IsAttached;
	private static MethodDefinition AntiDebug_1;

	internal static void Register(VMContext ctx)
	{
		if (ForceDisable) return;

		Exit = ctx.core.module.DefaultImporter.ImportMethod(typeof(Environment)
			.GetMethod("_Exit", BindingFlags.Static | BindingFlags.NonPublic));
		ExitIdx = ctx.Transform((MetadataMember)Exit);

		var w = HighLevelIced.Get(ctx);
		w.KickDnspy();
		w.asm.AddInstruction(Instruction.Create(Code.Mov_RAX_moffs64, Iced.Intel.Register.RAX, 
			new MemoryOperand(Iced.Intel.Register.None, 0x60, 8, false, Iced.Intel.Register.GS)));
		w.asm.mov(al, __[rax+0xBC]);
		w.asm.and(al, 0x70);
		w.asm.cmp(al, 0x70);
		var stopHIM = w.asm.CreateLabel();
		w.asm.jz(stopHIM);
		w.asm.ret();
		w.asm.Label(ref stopHIM);
		w.asm.jmp(w.Arg1_64);
		PebCheck = ctx.AllocNativeMethod("a", MethodSignature.CreateStatic(ctx.core.module.CorLibTypeFactory.Void, ctx.PTR)).Owner;
		PebCheck.NativeMethodBody.Code = w.Compile();
		PebCheckIdx = ctx.Transform(PebCheck);

		IsAttached = ctx.core.module.DefaultImporter.ImportMethod(typeof(System.Diagnostics.Debugger)
			.GetMethod("get_IsAttached"));

		w = HighLevelIced.Get(ctx);
		w.KickDnspy();
		stopHIM = w.asm.CreateLabel();
		w.asm.call(w.Arg1_64);
		w.asm.cmp(al, 1); // Debugger.IsAttached == true
		w.asm.jz(stopHIM);
		
		w.asm.mov(al, __byte_ptr[w.Arg1_64]); // Debugger.IsAttached first bytes is 0x33?
		w.asm.cmp(al, 0x33);
		w.asm.jz(stopHIM); // good try, DnSpy

		// oh no, x64dbg dodges us

		/* // x64dbg 2 : 1 me :(
		var isdbg = w.AddData(0);
		w.asm.mov(rax, __qword_ptr[isdbg]);
		w.asm.call(rax); // CRAAAAAAAAAAAAAASH AAAAAAAAAAAAGRH
		w.asm.cmp(al, 1);
		w.asm.jz(stopHIM); // bonk x64dbg if there no any bypass

		w.asm.mov(rax, __qword_ptr[isdbg]);
		w.asm.mov(al, __byte_ptr[rax]); // oh wait
		w.asm.cmp(al, 0x33);
		w.asm.jz(stopHIM); // bonk again
		*/

		w.asm.ret();

		w.asm.Label(ref stopHIM);
		w.asm.jmp(w.Arg2_64);

		AntiDebug_1 = ctx.AllocNativeMethod("a", MethodSignature.CreateStatic(ctx.core.module.CorLibTypeFactory.Void, ctx.PTR, ctx.PTR)).Owner;
		AntiDebug_1.NativeMethodBody.Code = w.Compile();
		//var kernel32 = new ImportedModule("kernel32.dll");
		//var isdebugged = new ImportedSymbol(0, "IsDebuggerPresent");
		//kernel32.Symbols.Add(isdebugged);
		//AntiDebug_1.NativeMethodBody.AddressFixups.Add(new AddressFixup((uint)(AntiDebug_1.NativeMethodBody.Code.Length-8), AddressFixupType.Absolute64BitAddress, isdebugged));
	}

	internal static void DoSomeDebugChecks(CilInstructionCollection i, VMContext ctx)
	{
		if (ForceDisable) return;
		if (!ctx.core.ApplyProtections) return;

		var pos = Random.Shared.Next(0, i.Count - 1);

		i.InsertLdftnHide(ref pos, ctx, IsAttached);
		i.InsertLdftnHide(ref pos, ctx, Exit);
		i.InsertCallHide(ref pos, ctx, AntiDebug_1);
	}

	internal static void OnVMCctorBuilded(VMContext ctx)
	{
		if (ForceDisable) return;
		var i = ctx.VMType.GetStaticConstructor().CilMethodBody.Instructions;
		i.LdftnHideOutsideVM(ctx, ExitIdx);
		i.CallHideOutsideVM(ctx, PebCheck, PebCheckIdx);
	}
}

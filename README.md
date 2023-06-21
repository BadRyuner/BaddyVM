# BaddyVM
Another poorly implemented virtual machine for C#.
Good for crackme obfuscation to show how good you are.

# What opcodes are implemented?
[Click me](https://github.com/BadRyuner/BaddyVM/blob/master/BaddyVM/VM/VMCore.cs#L115)

# How well does it work?
Can add two times two and coolly call a virtual method that no one will know about it. (Using Washi theory) (It won't work with interfaces, coreclr will fall)
It cant eat methods that returns ValueTypes larger than 8 bytes.
Not all opcodes work correctly. 
_*Made for fun.*_

# How much slower will the code run?
Slower than the Yandere-dev code

# How to use?
Download latest sources, open Program.cs, change it for yourself and compile it in Release (because of the #!DEBUG directive, which turns off part of the protection in the debug release to make debugging easier for me) mode.

# VM in action
[Crackme #1 (Used old version)](https://crackmes.one/crackme/648c69d033c5d4393891394c)

# How it looks
```csharp
public unsafe static void Main(string[] args)
{
	int* ptr = Marshal.AllocHGlobal(64);
	int* ptr2 = Marshal.AllocHGlobal(120);
	*(IntPtr*)(ptr2 + 0) = ptr;
	*(IntPtr*)(ptr2 + 12) = args;
	*(IntPtr*)(ptr2 + 10) = 1;
	*(IntPtr*)(ptr2 + 4) = VMRunner.VMTable;
	VMRunner.a(<Module>.Main(), ptr2);
	Marshal.FreeHGlobal(ptr);
	Marshal.FreeHGlobal(ptr2);
}
```
The VM instructions look like this
```csharp
internal unsafe static int* Store(int* A_0, int* A_1)
{
	int num = (int)(*(ushort*)A_0);
	A_0 = (int*)((byte*)A_0 + 2);
	ref IntPtr ptr = ref *(IntPtr*)(A_1 + num / 4);
	int* ptr2 = *(long*)(A_1 + 0);
	IntPtr intPtr = (IntPtr)(*(long*)ptr2);
	ptr2 -= 2;
	*(IntPtr*)(A_1 + 0) = ptr2;
	ptr = intPtr;
	jmp(Router());
}
```
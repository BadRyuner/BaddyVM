namespace Crackme;

public class Program
{
	public Program()
	{
		b = 1;
		f = new Foo() { a = 2, b = 3, c = 4 };
		//f.a = 2; f.b = 3; f.c = 4;
		h = 5;
	}

	public void Print()
	{
		Console.WriteLine($"{b} {f.a} {f.b} {f.c} {h}");
	}

	private static void Main(string[] args)
	{
		new Program().Print();
		//Console.ReadKey();
	}
	
	byte b;
	Foo f;
	int h;
}

public struct Foo
{
	public int a;
	public byte b;
	public long c;
}
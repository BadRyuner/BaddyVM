namespace Crackme;

public unsafe class Program
{
	public Program(int a, int b, int c)
	{
		Console.WriteLine($"{a} - {b} - {c}");
		Console.WriteLine(this.GetType());
	}

	public int aboba;
	public long kek;

	public Program()
	{
		aboba = 123;
		kek = 456;
	}

	public static void Main(string[] args)
	{
		//var r = DoSomething();
		//Console.WriteLine(r == null);
		//Test(r);
		//Test1(20, 40);
		//foreach(var i in new int[] { 1,2,3,4,5,6,7,8,9,10 }) // no crash
		//	Console.WriteLine(i);

		//var list = new List<int>() { 1,2,3,4,5,6,7 };
		//foreach(var i in list) // crash, thx Enumerator ValueType
		//	Console.WriteLine(i);

		var p = new Program();
		Console.WriteLine($"{p.aboba}, {p.kek}");
		p.aboba *= 2;
		Console.WriteLine(p.aboba);
		ref var k = ref AnotherTest(p);
		k += 7; // bad
		Console.WriteLine(k);
	}

	static ref long AnotherTest(Program p)
	{
		return ref p.kek;
	}

	static int ab = 0;

	private static void Test1(int a, int b)
	{
		new Program(634, 32, 754);
		Test2(634, 32, 754);
		//Console.WriteLine($"a = {a}, b = {b}");
	}

	private static void Test2(int a, int b, int c)
	{
		Console.WriteLine($"{a} - {b} - {c}");
	}

	public static void Test(Program p)
	{
		//p.NonVirtKek();
		p.Kek();
	}

	private static Program DoSomething()
	{
		Test1(77, 66);
		var a = new Program(625, 982, 32);
		return a;
	}

	public virtual void Kek()
	{
		Console.WriteLine("kek");
	}

	public virtual void Kek1()
	{
		Console.WriteLine("kek1");
	}

	public virtual void Kek2()
	{
		Console.WriteLine("kek2");
	}

	public virtual void Kek3()
	{
		Console.WriteLine("kek3");
	}

	public virtual void Kek4()
	{
		Console.WriteLine("kek4");
	}

	public virtual void Kek5()
	{
		Console.WriteLine("kek5");
	}
}

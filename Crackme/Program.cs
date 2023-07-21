using Terminal.Gui;

namespace Crackme;

public unsafe class Program
{
	public class A
	{
		public virtual void a1() => Console.WriteLine(1);
		public virtual void a2() => Console.WriteLine(2);
		public virtual void a3() => Console.WriteLine(3);
		public virtual void a4() => Console.WriteLine(4);
		public virtual void a5() => Console.WriteLine(5);
		public virtual void a6() => Console.WriteLine(6);
		public virtual void a7() => Console.WriteLine(7);
		public virtual void a8() => Console.WriteLine(8);
		public virtual void a9() => Console.WriteLine(9);
		public virtual void a10() => Console.WriteLine(10);
		public virtual void a11() => Console.WriteLine(11);
		public virtual void a12() => Console.WriteLine(12);
		public virtual void a13() => Console.WriteLine(13);
	}

	public class B : A
	{
		public override void a10() => Console.WriteLine(100);
		public override void a11() => Console.WriteLine(110);
		public override void a12() => Console.WriteLine(120);
		public new void a13() => Console.WriteLine(1113);
	}

	public static A G() => new B();

	static void Test()
	{
		var a = G();
		a.a1();
		a.a2();
		a.a3();
		a.a4();
		a.a5();
		a.a6();
		a.a7();
		a.a8();
		a.a9();
		a.a10();
		a.a11();
		a.a12();
		a.a13();
		((B)a).a13();
	}

	static void Test2(kek k)
	{
		Console.WriteLine(k.a);
		//Console.WriteLine(k.b);
	}

	//static int a = 1;

	struct kek
	{
		public int a;
	}
	
	private static void Main(string[] args)
	{
		//int a = 1;
		//Test2(new kek() { a = 1 });
		//Console.WriteLine(a);
		//Console.WriteLine(Int64.Parse("52748"));
		//Test();
		Console.WriteLine(Console.ReadLine() == "MrJoposranchik");
		//Start();
	}

	public static void Start()
	{
		Application.Init();
		var window = new Window("Crackme") { X = Pos.Center(), Y = Pos.Center(), Width = 42, Height = 10 };
		window.ColorScheme.Normal = new Terminal.Gui.Attribute(Color.White, Color.Black);

		Label p = null;
		var labels = new Label[8];
		for (int i = 0; i < 8; i++)
		{
			if (p == null)
				p = new Label("0") { X = 1, Y = 3, TextAlignment = TextAlignment.Centered, Width = 3 };
			else
				p = new Label("0") { X = Pos.Right(p) + 2, Y = 3, TextAlignment = TextAlignment.Centered, Width = 3 };
			p.Border = new Border() { BorderStyle = BorderStyle.Rounded, BorderBrush = Color.White, Background = Color.Black };
			labels[i] = p;
			window.Add(p);
			var iCopy = i;
			var up = new Button("\u2191") { X = p.X - 1, Y = 1 };
			up.Border = new Border() { BorderBrush = Color.White, Background = Color.Black };
			up.Clicked += () =>
			{
				var current = int.Parse(labels[iCopy].Text.ToString());
				if (current <= 8)
					labels[iCopy].Text = ((char)(current + 1 + '0')).ToString();
				else
					labels[iCopy].Text = "0";
			};
			var down = new Button("\u2193") { X = p.X - 1, Y = 5 };
			down.Border = new Border() { BorderBrush = Color.White, Background = Color.Black };
			down.Clicked += () =>
			{
				var current = int.Parse(labels[iCopy].Text.ToString());
				if (current > 0)
					labels[iCopy].Text = ((char)(current - 1 + '0')).ToString();
				else
					labels[iCopy].Text = "9";
			};
			window.Add(up);
			window.Add(down);
		}

		var button = new Button("Open") { X = Pos.Center(), Y = 7 };
		button.Clicked += () =>
		{
			bool good = false;
			var p1 = long.Parse(labels[0].Text.ToString());
			var p2 = long.Parse(labels[1].Text.ToString());
			long pin = p1;
			pin |= p2 << 8;
			if (pin != 0b0000_0011_0000_0110)
				goto ohno;
			pin |= long.Parse(labels[2].Text.ToString()) << 16;
			pin |= long.Parse(labels[3].Text.ToString()) << 24;
			pin |= long.Parse(labels[4].Text.ToString()) << 32;
			pin |= long.Parse(labels[5].Text.ToString()) << 40;
			pin |= long.Parse(labels[6].Text.ToString()) << 48;
			pin |= long.Parse(labels[7].Text.ToString()) << 56;

			good = pin == 285881730728710;
		ohno:
			if (good)
				MessageBox.Query("Mission Completed!", ":)");
			else
				MessageBox.Query("Bad Pin!", "Try Again!");
		};

		window.Add(button);
		Application.Top.Add(window);
		Application.Run(AlwaysTrue);
		Application.Shutdown();
	}

	static bool AlwaysTrue(Exception e)
	{
		MessageBox.Query("Er", e.Message);
		return true;
	}
}
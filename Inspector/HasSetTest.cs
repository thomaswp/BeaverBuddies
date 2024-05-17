using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Inspector
{
    internal class HasSetTest
    {
        static void Test()
        {
            Random rand = new Random(1234);
            HashSet<string> set = new HashSet<string>(10);
            List<string> list = new List<string>();
            for (int i = 0; i < 30; i++)
            {
                //Test t = new Test();
                //t.x = i;
                //list.Add(t);
                list.Add(i.ToString());
            }
            for (int i = 0; i < 10000; i++)
            {
                var item = list[rand.Next(list.Count)];
                if (rand.Next(2) == 0)
                {
                    set.Add(item);
                }
                else
                {
                    set.Remove(item);
                }
            }
            var e = set.GetEnumerator();
            while (e.MoveNext())
            {
                Console.WriteLine(e.Current);
            }
            int hash = 13;
            foreach (var t in set)
            {
                //Console.WriteLine($"{t.x}");
                //hash = hash * 7 + t.x;
                hash = hash * 7 + int.Parse(t);
            }
            Console.WriteLine("---");
            Console.WriteLine(set.First().GetHashCode());
            Console.WriteLine(hash);
            Console.WriteLine(set.First());
            Console.WriteLine(set.Last());
            Console.WriteLine(set.ElementAt(set.Count / 2));
        }
    }
}

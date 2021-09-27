using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace BranchReload2
{
    public class AsinkOverseer
    {
        
        public List<Action> CheckYieldBag { get; set; } = new ();
        public ManualResetEvent YieldUntil(Expression<Func<bool>> expression)
        {
            
            var reset = new ManualResetEvent(false);
            void Item()
            {
                if (expression.Compile().Invoke())
                {
                    lock (CheckYieldBag) CheckYieldBag.Remove(Item);
                    reset.Set();
                }
            }

            lock (CheckYieldBag) CheckYieldBag.Add(Item);

            return reset;
        }
        

        public async Task CheckYields()
        {
            int i = 0;
            try
            {
                while (true)
                {
                    await Task.Delay(10);
                    int count = 0;
                    Action action;
                    
                    lock (CheckYieldBag)
                    {
                        action = CheckYieldBag.ElementAtOrDefault(i);
                        count = CheckYieldBag.Count;
                    }

                    action?.Invoke();

                    i++;
                    if (count == 0)
                        i = 0;
                    else
                        i %= count;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
        
        public void WaitUntil(Expression<Func<bool>> expression) => YieldUntil(expression).WaitOne();
        
        public IEnumerable<string> DequeueEach(IList<string> items, string flag = "COMPLETED")
        {
            int position = 0;
            int count = 0;

            while (true)
            {
                YieldUntil(() => count != items.Count).WaitOne();
                lock (items)
                {
                    count = items.Count;
                    while (position < count)
                    {
                        string response = items[position++];
                        if (response.Equals(flag))
                            yield break;
                        yield return response;
                    }
                }
            }
        }
    }
}
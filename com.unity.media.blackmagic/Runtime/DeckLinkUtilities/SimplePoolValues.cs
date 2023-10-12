using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;

namespace Unity.Media.Blackmagic
{
    class SimpleValuePool<T> : KeyedCollection<T, T>
        where T : struct, IConvertible, IComparable<T>
    {
        T m_NextAvailable;
        Func<T, T> m_IncrementFunc;
        HashSet<T> m_UsedValues;

        public SimpleValuePool() : base()
        {
            // Cannot use 'dynamic' keyword in Unity
            var parameter = Expression.Parameter(typeof(T), "x");
            var increment = Expression.Increment(parameter);
            m_IncrementFunc = Expression.Lambda<Func<T, T>>(increment, parameter).Compile();

            m_UsedValues = new HashSet<T>();
            m_NextAvailable = m_IncrementFunc(m_NextAvailable);
        }

        public T GetNextValue()
        {
            // Can we reuse a value available in the pool.
            if (Count > 0 && Items.First().CompareTo(m_NextAvailable) < 0)
            {
                var itemIndex = Items.First();
                Remove(itemIndex);
                m_UsedValues.Add(itemIndex);
                return itemIndex;
            }

            // Have we already used a value in a previously saved configuration.
            while (m_UsedValues.Count > 0 && m_UsedValues.Contains(m_NextAvailable))
            {
                m_NextAvailable = m_IncrementFunc(m_NextAvailable);
            }

            // Simply iterate to the next value.
            var nextValue = m_NextAvailable;
            m_NextAvailable = m_IncrementFunc(nextValue);

            return nextValue;
        }

        public void ReturnValue(T returnedValue)
        {
            m_UsedValues.Remove(returnedValue);

            // You don't need to add the value to the pool as it's already
            // lower than the current iterator.
            if (returnedValue.CompareTo(m_NextAvailable) > 0)
                return;

            Add(returnedValue);

            // We sort the KeyedCollection Dictionary to always get the lowest value first.
            // OrderBy is not changing the actual list.
            var listOrdered = Items.OrderBy(i => i).ToList();
            ClearItems();
            listOrdered.ForEach(x => Add(x));
        }

        public void AddUsedValue(T usedValue)
        {
            m_UsedValues.Add(usedValue);
        }

        protected override T GetKeyForItem(T item)
        {
            return item;
        }

        protected override void ClearItems()
        {
            base.ClearItems();

            Items.Clear();
            Dictionary.Clear();
        }
    }
}

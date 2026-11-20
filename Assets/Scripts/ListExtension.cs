using System.Collections.Generic;

static class ListExtension
{   
    private static readonly System.Random Rng = new System.Random();
    public static void Shuffle<T>(this IList<T> list)
    {
        list.Shuffle(Rng);
    }

    public static void Shuffle<T>(this IList<T> list, System.Random random)
    {
      if (random == null)
        throw new System.ArgumentNullException("random");

      int n = list.Count;
      while (n > 1)
      {
        n--;
        int k = random.Next(n + 1);
        T value = list[k];
        list[k] = list[n];
        list[n] = value;
      }
    }
}
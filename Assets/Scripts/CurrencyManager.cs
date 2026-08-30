using System;
using UnityEngine;

public class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance { get; private set; }

    [Header("Starting Balance")]
    public int startingCoins = 100;

    public int CurrentCoins { get; private set; }

    public event Action<int> OnCoinsChanged;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        CurrentCoins = startingCoins;
    }

    public void AddCoins(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        CurrentCoins += amount;
        OnCoinsChanged?.Invoke(CurrentCoins);
    }

    public bool TrySpendCoins(int amount)
    {
        if (amount <= 0 || CurrentCoins < amount)
        {
            return false;
        }

        CurrentCoins -= amount;
        OnCoinsChanged?.Invoke(CurrentCoins);
        return true;
    }
}
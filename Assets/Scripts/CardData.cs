using UnityEngine;

public enum Suit { Clubs, Diamonds, Hearts, Spades }

[System.Serializable]
public class CardData
{
    public Suit suit;
    public int rank; // 1 (Ace) to 13 (King)
    public bool isFaceUp;

    public CardData(Suit suit, int rank)
    {
        this.suit = suit;
        this.rank = rank;
        this.isFaceUp = false;
    }
}
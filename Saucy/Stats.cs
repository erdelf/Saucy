using FFTriadBuddy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TriadBuddyPlugin;

namespace Saucy
{
    public class Stats
    {
        public int GamesPlayedWithSaucy = 0;

        public int GamesWonWithSaucy = 0;

        public int GamesLostWithSaucy = 0;

        public int GamesDrawnWithSaucy = 0;

        public int CardsDroppedWithSaucy = 0;

        public int MGPWon = 0;

        public Dictionary<string, int> NPCsPlayed = new Dictionary<string, int>();

        public Dictionary<uint, int> CardsWon = new Dictionary<uint, int>();

        public List<TriadMatchRecord> recordedMatches = new List<TriadMatchRecord>();
    }

    public struct TriadMatchRecord
    {
        public DateTime date;
        public int      deck;
        public int[]    deckCards;
        public string   npc;
        public int      mgp;
        public int      card;
        public double   time;

        public GameEndState result;

        public TriadCard GetWonCard() =>
            TriadCardDB.Get().FindById(card);

        public TriadCard[] GetResolvedDeckCards()
        {
            TriadCard[] cards = new TriadCard[this.deckCards.Length];

            
            for (int i = 0; i < cards.Length; i++)
            {
                TriadCard triadCard = TriadCardDB.Get().FindById(this.deckCards[i]);
                cards[i] = triadCard;
            }

            return cards;
        }

        public TriadMatchRecord()
        {
        }
    }

    public enum GameEndState
    {
        Lost,
        Draw,
        Won
    }
}

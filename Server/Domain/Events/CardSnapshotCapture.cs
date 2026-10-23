using System;
using ShadowverseBeyond.Server.Domain.Entities;

namespace ShadowverseBeyond.Server.Domain.Events
{
    internal static class CardSnapshotCapture
    {
        public static CardInstanceSnapshot? TryCapture(CardInstance? card)
        {
            if (card == null)
            {
                return null;
            }

            try
            {
                return CardInstanceSnapshot.From(card);
            }
            catch
            {
                return null;
            }
        }

        public static CardInstanceSnapshot Capture(CardInstance card)
        {
            if (card == null)
            {
                throw new ArgumentNullException(nameof(card));
            }

            return CardInstanceSnapshot.From(card);
        }
    }
}

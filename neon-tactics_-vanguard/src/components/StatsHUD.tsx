
import React from 'react';
import { motion } from 'framer-motion';
import { PlayerState } from '../types';
import { MAX_PP_LIMIT } from '../constants';

interface StatsHUDProps {
  player: PlayerState;
  isPlayer: boolean;
  isActive: boolean;
  onMouseUp?: () => void;
}

export const StatsHUD: React.FC<StatsHUDProps> = ({ player, isPlayer, isActive, onMouseUp }) => {
  return (
    <div 
      className={`
        relative w-72 font-mono transition-opacity duration-300
        ${isActive ? 'opacity-100' : 'opacity-70'}
      `}
      onMouseUp={onMouseUp}
    >
      {/* Header Name & HP */}
      <div className="flex justify-between items-end border-b border-zinc-700 pb-1 mb-2 select-none">
        <div>
           <div className="text-[10px] text-zinc-500 uppercase tracking-widest">{player.class}</div>
           <div className="text-lg font-black text-white">{isPlayer ? 'YOU' : 'OPPONENT'}</div>
        </div>
        <div className="text-right">
            <motion.span 
               key={player.hp}
               initial={{ scale: 1.5, color: '#dc2626' }}
               animate={{ scale: 1, color: '#ffffff' }}
               className="text-2xl font-bold text-white inline-block"
            >
               {player.hp}
            </motion.span>
            <span className="text-xs text-zinc-500 ml-1">HP</span>
        </div>
      </div>

      {/* Resources Grid */}
      <div className="grid grid-cols-2 gap-2 text-xs">
         
         {/* PP Meter */}
         <div className="col-span-2">
            <div className="flex justify-between mb-1">
               <span className="text-blue-400 font-bold">PP</span>
               <span className="text-white">{player.pp} / {player.maxPp}</span>
            </div>
            <div className="flex gap-[2px] h-2">
               {Array.from({ length: MAX_PP_LIMIT }).map((_, i) => (
                  <div key={i} className={`
                     flex-1 rounded-sm transition-all
                     ${i < player.pp ? 'bg-blue-500 shadow-[0_0_5px_rgba(59,130,246,0.5)]' : 
                       i < player.maxPp ? 'bg-zinc-700' : 'bg-zinc-900 border border-zinc-800'}
                  `} />
               ))}
            </div>
         </div>

         {/* EP Points */}
         <div className="bg-zinc-900 p-1 border border-zinc-800 flex justify-between items-center px-2">
            <span className="text-yellow-500 font-bold">EP</span>
            <div className="flex gap-1">
                {Array.from({ length: 3 }).map((_, i) => (
                    <div key={i} className={`w-2 h-2 rounded-full ${i < player.ep ? 'bg-yellow-400' : 'bg-zinc-800'}`} />
                ))}
            </div>
         </div>

         {/* Shadows/Graveyard */}
         <div className="bg-zinc-900 p-1 border border-zinc-800 flex justify-between items-center px-2">
            <span className="text-purple-500 font-bold">SHADOWS</span>
            <span className="text-white">{player.shadows}</span>
         </div>
         
         {/* Deck Count */}
         <div className="bg-zinc-900 p-1 border border-zinc-800 flex justify-between items-center px-2">
            <span className="text-zinc-500 font-bold">DECK</span>
            <span className="text-white">{player.deckCount}</span>
         </div>

      </div>
    </div>
  );
};

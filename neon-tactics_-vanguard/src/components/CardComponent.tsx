
import React from 'react';
import { motion } from 'framer-motion';
import { RuntimeCard, CardType } from '../types';

interface CardProps {
  data: RuntimeCard;
  onClick?: () => void;
  disabled?: boolean;
  isPlayable?: boolean;
  isBoard?: boolean; // Different styling if on board
  isEnemy?: boolean; // Hide details if enemy hand
}

export const CardComponent: React.FC<CardProps> = ({ 
  data, onClick, disabled, isPlayable, isBoard, isEnemy 
}) => {
  
  if (isEnemy && !isBoard) {
    // Card Back for Enemy Hand
    return (
      <div className="w-32 h-48 bg-zinc-900 border border-zinc-700 rounded-sm relative overflow-hidden shadow-md transform rotate-180">
         <div className="absolute inset-0 bg-[radial-gradient(circle_at_center,_var(--tw-gradient-stops))] from-zinc-800 to-black" />
         <div className="absolute inset-2 border border-dashed border-zinc-700 opacity-50" />
      </div>
    );
  }

  const isFollower = data.type === CardType.FOLLOWER;
  const isSpell = data.type === CardType.SPELL;
  const isAmulet = data.type === CardType.AMULET;

  // Visual Flair based on Rarity/Evolved
  const borderColor = data.isEvolved ? 'border-yellow-400' : isPlayable ? 'border-white' : 'border-zinc-700';
  const textColor = isPlayable || isBoard ? 'text-white' : 'text-zinc-500';
  const bgColor = isBoard ? 'bg-black' : 'bg-zinc-900';

  return (
    <motion.div
      layout
      initial={isBoard ? { scale: 0.8, opacity: 0 } : { opacity: 0, y: 20 }}
      animate={{ scale: 1, opacity: 1, y: 0 }}
      whileHover={!disabled ? { scale: 1.05, zIndex: 10, y: -10 } : {}}
      onClick={!disabled ? onClick : undefined}
      className={`
        relative flex flex-col select-none group
        ${isBoard ? 'w-28 h-36' : 'w-40 h-56'}
        ${bgColor} border ${borderColor}
        transition-colors duration-300 shadow-lg
        ${disabled && !isBoard ? 'opacity-60 grayscale' : ''}
        ${data.canAttack && isBoard ? 'ring-2 ring-accent cursor-crosshair' : ''}
      `}
    >
      {/* Cost (Top Left) */}
      <div className="absolute -top-3 -left-3 w-8 h-8 bg-blue-600 text-white font-mono font-bold flex items-center justify-center rounded-full border-2 border-black z-20">
        {data.currentCost}
      </div>

      {/* Card Art Placeholder (Abstract Shapes based on ID) */}
      <div className="flex-1 relative overflow-hidden bg-zinc-800 m-1">
         <div className={`absolute inset-0 opacity-20 ${isSpell ? 'bg-purple-500' : isAmulet ? 'bg-green-500' : 'bg-zinc-500'}`} />
         {/* Abstract Icon */}
         <div className="absolute inset-0 flex items-center justify-center text-4xl font-black text-zinc-700 opacity-50">
            {data.name.substring(0,2).toUpperCase()}
         </div>
      </div>

      {/* Text Area */}
      <div className="p-2 relative z-10 bg-zinc-900/90 backdrop-blur-sm border-t border-zinc-800 h-[40%] flex flex-col justify-between">
         <div>
            <h3 className={`font-bold uppercase leading-none tracking-tight ${isBoard ? 'text-[10px]' : 'text-xs'} ${textColor}`}>
              {data.name}
            </h3>
            {!isBoard && (
               <div className="text-[8px] font-mono text-zinc-500 mt-1 leading-tight line-clamp-3">
                  {data.effects.map(e => e.split('|')[0]).join(' ')}
               </div>
            )}
         </div>
         
         <div className="flex justify-between items-center w-full text-[10px] font-mono text-zinc-600 uppercase">
            <span>{data.type}</span>
            <span>{data.rarity}</span>
         </div>
      </div>

      {/* Stats (Bottom for Followers) */}
      {isFollower && (
        <div className="absolute -bottom-3 w-full flex justify-between px-1 z-20">
           <div className={`w-8 h-8 bg-zinc-200 text-black font-black flex items-center justify-center border-2 border-black rounded-sm ${data.isEvolved ? 'bg-yellow-400' : ''}`}>
             {data.currentAttack}
           </div>
           <div className={`w-8 h-8 bg-zinc-200 text-black font-black flex items-center justify-center border-2 border-black rounded-full ${data.isEvolved ? 'bg-yellow-400' : ''}`}>
             {data.currentDefense}
           </div>
        </div>
      )}
      
      {/* Evolved Visual Effect */}
      {data.isEvolved && (
        <div className="absolute inset-0 border-2 border-yellow-400/50 pointer-events-none animate-pulse" />
      )}
    </motion.div>
  );
};

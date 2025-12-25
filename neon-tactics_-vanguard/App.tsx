import React, { useEffect, useState, useRef } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { ArrowRight, RefreshCw, Terminal } from 'lucide-react';
import { useGameStore } from './src/store';
import { StatsHUD } from './src/components/StatsHUD';
import { CardComponent } from './src/components/CardComponent';

interface BoardSlotProps {
  card: any;
  index: number;
  isPlayer: boolean;
  onMouseDown?: (index: number, e: React.MouseEvent) => void;
  onMouseUp?: (index: number, e: React.MouseEvent) => void;
}

// Board Slot Component
const BoardSlot: React.FC<BoardSlotProps> = ({
  card,
  index,
  isPlayer,
  onMouseDown,
  onMouseUp
}) => {
  const { player, currentTurn, evolve } = useGameStore();

  // Can this slot be interacted with?
  // Player slots: Drag start (MouseDown)
  // Enemy slots: Drop target (MouseUp)

  return (
    <div
      className={`
        w-28 h-36 border border-dashed border-zinc-800 rounded-md flex items-center justify-center relative group
        ${!isPlayer && card ? 'hover:border-accent hover:border-solid transition-colors' : ''}
      `}
      onMouseDown={(e) => {
        if (isPlayer && onMouseDown) onMouseDown(index, e);
      }}
      onMouseUp={(e) => {
        if (!isPlayer && onMouseUp) onMouseUp(index, e);
      }}
    >
      {card ? (
        <div className="relative pointer-events-none">
          <div className="pointer-events-auto">
            <CardComponent
              data={card}
              isBoard
              disabled={!isPlayer && currentTurn === 'player'}
            />
          </div>

          {/* Evolve Trigger (Keep simple button for now) */}
          {isPlayer && !card.isEvolved && player.ep > 0 && !card.hasAttacked && (
            <button
              onClick={(e) => { e.stopPropagation(); evolve(index); }}
              className="absolute -right-4 -top-4 bg-yellow-500 text-black text-[8px] font-bold px-2 py-1 rounded-full opacity-0 group-hover:opacity-100 transition-opacity pointer-events-auto z-30"
            >
              EVOLVE
            </button>
          )}
        </div>
      ) : (
        <div className="text-zinc-800 text-xs">+</div>
      )}
    </div>
  );
};

export default function App() {
  const {
    initGame,
    player,
    opponent,
    currentTurn,
    winner,
    logs,
    playCard,
    endTurn,
    attack
  } = useGameStore();

  // Expose store for debugging
  useEffect(() => {
    (window as any).gameStore = useGameStore;
  }, []);

  // --- Dragging Logic ---
  const [dragState, setDragState] = useState<{
    active: boolean;
    startIndex: number | null;
    startCoords: { x: number; y: number };
    currentCoords: { x: number; y: number };
  }>({ active: false, startIndex: null, startCoords: { x: 0, y: 0 }, currentCoords: { x: 0, y: 0 } });

  const svgRef = useRef<SVGSVGElement>(null);

  const handleDragStart = (index: number, e: React.MouseEvent) => {
    if (currentTurn !== 'player') return;
    const card = player.board[index];
    if (!card || !card.canAttack) return;

    // Don't start drag if clicking buttons
    if ((e.target as HTMLElement).tagName === 'BUTTON') return;

    e.preventDefault(); // Prevent text selection
    setDragState({
      active: true,
      startIndex: index,
      startCoords: { x: e.clientX, y: e.clientY },
      currentCoords: { x: e.clientX, y: e.clientY }
    });
  };

  const handleMouseMove = (e: React.MouseEvent) => {
    if (!dragState.active) return;
    setDragState(prev => ({
      ...prev,
      currentCoords: { x: e.clientX, y: e.clientY }
    }));
  };

  const handleGlobalMouseUp = () => {
    if (dragState.active) {
      setDragState({ active: false, startIndex: null, startCoords: { x: 0, y: 0 }, currentCoords: { x: 0, y: 0 } });
    }
  };

  const handleTargetDrop = (targetIndex: number | 'leader') => {
    if (dragState.active && dragState.startIndex !== null) {
      attack(dragState.startIndex, targetIndex);
      // State will be cleared by global mouse up, but we can force it here to be snappy
      setDragState({ active: false, startIndex: null, startCoords: { x: 0, y: 0 }, currentCoords: { x: 0, y: 0 } });
    }
  };

  useEffect(() => {
    initGame();
  }, []);

  return (
    <div
      className="relative w-full h-screen bg-background text-primary flex flex-col overflow-hidden select-none cursor-default"
      onMouseMove={handleMouseMove}
      onMouseUp={handleGlobalMouseUp}
    >

      {/* Background Grid */}
      <div className="absolute inset-0 bg-[size:40px_40px] bg-grid-pattern opacity-[0.05] pointer-events-none" />

      {/* Attack Line SVG Overlay */}
      {dragState.active && (
        <svg ref={svgRef} className="absolute inset-0 w-full h-full pointer-events-none z-50 drop-shadow-[0_0_8px_rgba(220,38,38,0.8)]">
          <defs>
            <marker id="arrowhead" markerWidth="10" markerHeight="7" refX="9" refY="3.5" orient="auto">
              <polygon points="0 0, 10 3.5, 0 7" fill="#dc2626" />
            </marker>
          </defs>
          <line
            x1={dragState.startCoords.x}
            y1={dragState.startCoords.y}
            x2={dragState.currentCoords.x}
            y2={dragState.currentCoords.y}
            stroke="#dc2626"
            strokeWidth="4"
            strokeDasharray="10,5"
            markerEnd="url(#arrowhead)"
            className="animate-[dash_1s_linear_infinite]"
          />
        </svg>
      )}

      <style>{`
        @keyframes dash {
          to {
            stroke-dashoffset: -15;
          }
        }
      `}</style>

      {/* --- OPPONENT AREA --- */}
      <div className="flex-1 flex flex-col pt-4 relative">
        <div className="flex justify-between px-8 items-start">
          <div className="w-80 hidden md:flex flex-col font-mono text-xs opacity-70 h-32 overflow-hidden">
            <div className="flex items-center gap-2 text-zinc-500 mb-1"><Terminal size={12} /> BATTLE_LOG</div>
            {logs.slice(0, 5).map(log => (
              <div key={log.id} className={`${log.type === 'error' ? 'text-red-500' : log.type === 'combat' ? 'text-white' : 'text-zinc-500'}`}>
                {'>'} {log.message}
              </div>
            ))}
          </div>

          {/* Opponent Hand (Face down) */}
          <div className="flex -space-x-12 items-start justify-center">
            {opponent.hand.map((c, i) => (
              <CardComponent key={c.instanceId} data={c} isEnemy />
            ))}
          </div>

          {/* Enemy Leader / HUD (Drop Target) */}
          <div className="relative group">
            <StatsHUD
              player={opponent}
              isPlayer={false}
              isActive={currentTurn === 'opponent'}
              onMouseUp={() => handleTargetDrop('leader')}
            />
            {dragState.active && (
              <div className="absolute inset-0 border-2 border-accent rounded-lg opacity-50 animate-pulse pointer-events-none" />
            )}
          </div>
        </div>

        {/* Opponent Board */}
        <div className="flex-1 flex items-center justify-center gap-4 mt-4">
          {opponent.board.map((card, i) => (
            <BoardSlot
              key={i}
              card={card}
              index={i}
              isPlayer={false}
              onMouseUp={(idx) => handleTargetDrop(idx)}
            />
          ))}
        </div>
      </div>

      {/* --- MID FIELD (Divider) --- */}
      <div className="h-px w-full bg-gradient-to-r from-transparent via-zinc-700 to-transparent my-2 opacity-50" />

      {/* --- PLAYER AREA --- */}
      <div className="flex-1 flex flex-col pb-4 relative">

        {/* Player Board */}
        <div className="flex-1 flex items-center justify-center gap-4 mb-4">
          {player.board.map((card, i) => (
            <BoardSlot
              key={i}
              card={card}
              index={i}
              isPlayer={true}
              onMouseDown={handleDragStart}
            />
          ))}
        </div>

        {/* Player Controls & Hand */}
        <div className="flex justify-between items-end px-8">
          <StatsHUD player={player} isPlayer={true} isActive={currentTurn === 'player'} />

          {/* Hand */}
          <div className="flex-1 flex justify-center items-end -mb-20 hover:mb-4 transition-all duration-300 z-20">
            <div className="flex -space-x-12 hover:-space-x-2 transition-all duration-300">
              {player.hand.map((card, i) => (
                <CardComponent
                  key={card.instanceId}
                  data={card}
                  isPlayable={currentTurn === 'player' && player.pp >= card.currentCost}
                  disabled={currentTurn !== 'player' || player.pp < card.currentCost}
                  onClick={() => playCard(i)}
                />
              ))}
            </div>
          </div>

          {/* End Turn Button */}
          <div className="w-80 flex justify-end pb-4">
            {currentTurn === 'player' && (
              <motion.button
                whileHover={{ scale: 1.05 }}
                whileTap={{ scale: 0.95 }}
                onClick={endTurn}
                className="px-6 py-3 bg-white text-black font-black font-mono text-sm flex items-center gap-2 border-4 border-white shadow-[0_0_20px_rgba(255,255,255,0.3)]"
              >
                END TURN <ArrowRight size={16} />
              </motion.button>
            )}
          </div>
        </div>
      </div>

      {/* Victory/Defeat Overlay */}
      <AnimatePresence>
        {winner && (
          <motion.div
            initial={{ opacity: 0 }} animate={{ opacity: 1 }}
            className="fixed inset-0 z-50 bg-black/90 flex items-center justify-center"
          >
            <div className="text-center">
              <h1 className="text-8xl font-black text-white tracking-tighter mb-8">
                {winner === 'player' ? 'VICTORY' : 'DEFEAT'}
              </h1>
              <button
                onClick={() => window.location.reload()}
                className="px-8 py-4 border border-zinc-500 text-zinc-300 hover:bg-white hover:text-black transition flex items-center gap-2 mx-auto font-mono"
              >
                <RefreshCw size={16} /> INITIALIZE_NEW_SESSION
              </button>
            </div>
          </motion.div>
        )}
      </AnimatePresence>

    </div>
  );
}
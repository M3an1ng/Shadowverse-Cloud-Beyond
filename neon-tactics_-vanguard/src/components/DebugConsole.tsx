
import React, { useState } from 'react';
import { useGameStore } from '../store';
import { PlayerState } from '../types';
import { Settings, X, Save, Download } from 'lucide-react';
import { motion, AnimatePresence } from 'framer-motion';

const PlayerDebugSection = ({ 
    player, 
    onUpdate 
}: { 
    player: PlayerState; 
    onUpdate: (updates: Partial<PlayerState>) => void;
}) => {
    const [values, setValues] = useState({
        hp: player.hp,
        maxHp: player.maxHp,
        pp: player.pp,
        maxPp: player.maxPp,
        ep: player.ep,
        shadows: player.shadows,
        rally: player.rally,
        cardsPlayed: player.cardsPlayedThisTurn
    });

    const handleChange = (key: keyof typeof values, val: string) => {
        const num = parseInt(val);
        if (isNaN(num)) return;
        setValues(prev => ({ ...prev, [key]: num }));
    };

    const handleSave = () => {
        onUpdate({
            hp: values.hp,
            maxHp: values.maxHp,
            pp: values.pp,
            maxPp: values.maxPp,
            ep: values.ep,
            shadows: values.shadows,
            rally: values.rally,
            cardsPlayedThisTurn: values.cardsPlayed
        });
    };

    return (
        <div className="mb-4 bg-zinc-900 p-2 rounded border border-zinc-700">
            <h4 className="text-xs font-bold mb-2 text-zinc-400 uppercase border-b border-zinc-800 pb-1">
                {player.playerId}
            </h4>
            <div className="grid grid-cols-2 gap-2 text-[10px]">
                <label className="flex flex-col">
                    <span className="text-zinc-500">HP / Max</span>
                    <div className="flex gap-1">
                        <input type="number" value={values.hp} onChange={e => handleChange('hp', e.target.value)} className="w-full bg-zinc-800 border border-zinc-700 p-1" />
                        <input type="number" value={values.maxHp} onChange={e => handleChange('maxHp', e.target.value)} className="w-full bg-zinc-800 border border-zinc-700 p-1" />
                    </div>
                </label>
                <label className="flex flex-col">
                    <span className="text-zinc-500">PP / Max</span>
                    <div className="flex gap-1">
                        <input type="number" value={values.pp} onChange={e => handleChange('pp', e.target.value)} className="w-full bg-zinc-800 border border-zinc-700 p-1" />
                        <input type="number" value={values.maxPp} onChange={e => handleChange('maxPp', e.target.value)} className="w-full bg-zinc-800 border border-zinc-700 p-1" />
                    </div>
                </label>
                <label className="flex flex-col">
                    <span className="text-zinc-500">EP</span>
                    <input type="number" value={values.ep} onChange={e => handleChange('ep', e.target.value)} className="bg-zinc-800 border border-zinc-700 p-1" />
                </label>
                <label className="flex flex-col">
                    <span className="text-zinc-500">Shadows</span>
                    <input type="number" value={values.shadows} onChange={e => handleChange('shadows', e.target.value)} className="bg-zinc-800 border border-zinc-700 p-1" />
                </label>
                <label className="flex flex-col">
                    <span className="text-zinc-500">Rally</span>
                    <input type="number" value={values.rally} onChange={e => handleChange('rally', e.target.value)} className="bg-zinc-800 border border-zinc-700 p-1" />
                </label>
                <label className="flex flex-col">
                    <span className="text-zinc-500">Combo</span>
                    <input type="number" value={values.cardsPlayed} onChange={e => handleChange('cardsPlayed', e.target.value)} className="bg-zinc-800 border border-zinc-700 p-1" />
                </label>
            </div>
            <button onClick={handleSave} className="mt-2 w-full bg-blue-900 hover:bg-blue-800 text-blue-200 text-[10px] py-1 flex items-center justify-center gap-1">
                <Save size={10} /> APPLY
            </button>
        </div>
    );
};

export const DebugConsole = () => {
    const [isOpen, setIsOpen] = useState(false);
    const { player, opponent, debugSetPlayerState, downloadLogs } = useGameStore();

    return (
        <>
            <button
                onClick={() => setIsOpen(!isOpen)}
                className="fixed bottom-4 right-4 z-[100] p-3 bg-zinc-800 text-zinc-400 hover:text-white rounded-full shadow-lg border border-zinc-700 transition-colors"
            >
                {isOpen ? <X size={20} /> : <Settings size={20} />}
            </button>

            <AnimatePresence>
                {isOpen && (
                    <motion.div 
                        initial={{ x: 300, opacity: 0 }}
                        animate={{ x: 0, opacity: 1 }}
                        exit={{ x: 300, opacity: 0 }}
                        className="fixed top-0 right-0 h-full w-72 bg-zinc-950/95 border-l border-zinc-800 shadow-2xl z-40 p-4 overflow-y-auto backdrop-blur-sm"
                    >
                        <h3 className="text-sm font-mono font-bold text-white mb-4 flex items-center gap-2">
                            <Settings size={14} /> DEBUG_CONSOLE
                        </h3>
                        
                        <PlayerDebugSection 
                            player={player} 
                            onUpdate={(u) => debugSetPlayerState('player', u)} 
                        />
                        
                        <PlayerDebugSection 
                            player={opponent} 
                            onUpdate={(u) => debugSetPlayerState('opponent', u)} 
                        />
                        
                        <div className="mt-4 p-2 bg-yellow-900/20 border border-yellow-900/50 rounded text-[10px] text-yellow-500 font-mono">
                            WARNING: Modifying state directly may cause desync with game logic if active effects are processing. Use at start of turn.
                        </div>

                        <button
                            onClick={downloadLogs}
                            className="mt-4 w-full bg-zinc-800 hover:bg-zinc-700 text-zinc-300 text-xs py-2 rounded border border-zinc-600 flex items-center justify-center gap-2"
                        >
                            <Download size={14} /> Download Logs (CSV)
                        </button>
                    </motion.div>
                )}
            </AnimatePresence>
        </>
    );
};

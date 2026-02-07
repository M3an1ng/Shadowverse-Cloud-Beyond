import React, { useState, useEffect, useRef } from 'react';

const App = () => {
  const [progress, setProgress] = useState(100);
  const [isHovered, setIsHovered] = useState(false);
  const [isPressed, setIsPressed] = useState(false);
  const timerRef = useRef(null);

  // 模拟回合倒计时
  useEffect(() => {
    timerRef.current = setInterval(() => {
      setProgress((prev) => (prev > 0 ? prev - 0.25 : 100));
    }, 50);
    return () => clearInterval(timerRef.current);
  }, []);

  const handleEndTurn = () => {
    setIsPressed(true);
    setTimeout(() => {
      setIsPressed(false);
      setProgress(100);
    }, 200);
  };

  // 状态分级计算
  const isWarning = progress < 50;
  const isCritical = progress < 20;

  // 计算文字脉冲频率
  const pulseSpeed = isCritical ? '0.6s' : '1.5s';

  return (
    <div className="flex flex-col items-center justify-center min-h-screen bg-[#050505] font-sans selection:bg-red-500 overflow-hidden">
      <style>
        {`
          @import url('https://fonts.googleapis.com/css2?family=Orbitron:wght@700;900&display=swap');
          
          .brushed-metal {
            background: linear-gradient(135deg, #ffffff 0%, #fcfcfc 45%, #e8e8e8 50%, #fcfcfc 55%, #ffffff 100%);
            background-size: 200% 200%;
            position: relative;
            transition: all 0.5s cubic-bezier(0.4, 0, 0.2, 1);
          }

          .brushed-metal::after {
            content: "";
            position: absolute;
            inset: 0;
            background-image: repeating-linear-gradient(
              45deg,
              rgba(0,0,0,0.02) 0px,
              rgba(0,0,0,0.02) 1px,
              transparent 1px,
              transparent 2px
            );
            pointer-events: none;
          }

          .bevel-heavy {
            box-shadow: 
              inset 1.5px 1.5px 0px #fff,
              inset -1.5px -1.5px 3px rgba(0,0,0,0.1),
              0 30px 60px -12px rgba(0, 0, 0, 0.8);
          }

          /* 文字脉冲效果 */
          @keyframes text-overload {
            0%, 100% { transform: scale(1); filter: drop-shadow(0 0 5px rgba(255, 0, 60, 0.2)); }
            50% { transform: scale(1.04); filter: drop-shadow(0 0 25px rgba(255, 0, 60, 0.8)); }
          }

          .animate-text-pulse {
            animation: text-overload ${pulseSpeed} infinite ease-in-out;
          }

          /* 底部指示灯槽：嵌入式设计 */
          .indicator-trench-bottom {
            background: #000000;
            box-shadow: 
              inset 0 3px 6px rgba(0,0,0,0.8),
              0 1px 0 rgba(255,255,255,0.7);
          }

          /* 霓虹发光文字 */
          .neon-text {
            color: #ff003c;
            text-shadow: 0 0 15px rgba(255, 0, 60, 0.7), 0 0 30px rgba(255, 0, 60, 0.3);
          }

          .normal-text {
            color: #0a0a0a;
            text-shadow: 2px 2px 0px #fff, -1px -1px 0px rgba(0,0,0,0.05);
          }
        `}
      </style>

      {/* 顶部标题装饰 */}
      <div className={`mb-10 text-[10px] tracking-[1.2em] font-black uppercase transition-colors duration-500 ${isCritical ? 'text-red-600' : 'text-white/10'}`}>
        Tactical Interface v3.5
      </div>

      <button
        onMouseEnter={() => setIsHovered(true)}
        onMouseLeave={() => setIsHovered(false)}
        onMouseDown={handleEndTurn}
        className={`relative group transition-all duration-500 transform ${isPressed ? 'scale-95' : 'hover:scale-[1.02]'}`}
      >
        {/* 背景动态氛围光 */}
        <div 
          className={`absolute -inset-24 rounded-full transition-all duration-1000 blur-[120px] ${
            isCritical ? 'bg-red-600/25 opacity-100' : isWarning ? 'bg-red-600/10 opacity-60' : 'bg-red-600/0 opacity-0'
          }`}
        ></div>

        {/* 核心按钮底座 */}
        <div className={`
          brushed-metal bevel-heavy w-[420px] h-[180px] rounded-lg flex flex-col items-center justify-center relative overflow-hidden border-2
          ${isCritical ? 'border-red-600/40' : 'border-transparent'}
        `}>
          
          {/* 装饰细节：左上/右上机械螺丝感 */}
          <div className="absolute top-3 left-3 w-1.5 h-1.5 rounded-full bg-black/10 shadow-inner"></div>
          <div className="absolute top-3 right-3 w-1.5 h-1.5 rounded-full bg-black/10 shadow-inner"></div>
          
          {/* 核心文字区域 */}
          <div className={`relative z-10 flex flex-col items-center transition-transform duration-300 mb-2 ${isCritical ? 'animate-text-pulse' : ''}`}>
            <h1 
              className={`font-['Orbitron'] text-6xl font-black italic tracking-tighter transition-all duration-500 
                ${isCritical ? 'neon-text' : isWarning ? 'text-red-800' : 'normal-text'}
              `}
            >
              END TURN
            </h1>
            
            {/* 动态状态标签 */}
            <div className={`mt-3 text-[11px] font-bold tracking-[0.6em] flex items-center gap-3 transition-all duration-500 ${
              isCritical ? 'text-red-500' : isWarning ? 'text-orange-600' : 'text-gray-400'
            }`}>
              <span className={`w-2 h-2 rounded-full ${isCritical ? 'bg-red-600 animate-ping' : 'bg-current opacity-40'}`}></span>
              {isCritical ? 'CORE_OVERLOAD' : isWarning ? 'LOW_TIME_DET' : 'SYSTEM_STABLE'}
            </div>
          </div>

          {/* 嵌入到底部的指示灯阵列 (替代进度条) */}
          <div className="absolute bottom-0 left-0 w-full h-[14px] indicator-trench-bottom flex items-center justify-center gap-2 px-10">
            {Array.from({ length: 14 }).map((_, i) => {
              const threshold = (i / 14) * 100;
              const isActive = progress > threshold;
              return (
                <div 
                  key={i}
                  className={`flex-1 h-[4px] rounded-[1px] transition-all duration-300 ${
                    isActive 
                      ? (isCritical ? 'bg-red-600 shadow-[0_0_10px_#ff003c]' : isWarning ? 'bg-orange-500 shadow-[0_0_5px_rgba(249,115,22,0.5)]' : 'bg-white/80 shadow-[0_0_3px_rgba(255,255,255,0.3)]') 
                      : 'bg-white/5'
                  }`}
                  style={{ 
                    opacity: isActive ? 1 : 0.15,
                    transform: isActive ? 'scaleY(1)' : 'scaleY(0.7)'
                  }}
                ></div>
              );
            })}
            
            {/* 前端光晕装饰（仅在活跃时） */}
            <div 
              className="absolute right-0 h-full w-20 bg-gradient-to-l from-red-600/20 to-transparent transition-opacity duration-500"
              style={{ opacity: isCritical ? 1 : 0 }}
            ></div>
          </div>

          {/* 交互扫光效果 */}
          <div className="absolute top-0 -left-[100%] w-full h-full bg-gradient-to-r from-transparent via-white/40 to-transparent skew-x-[-25deg] group-hover:left-[100%] transition-all duration-1000 ease-in-out"></div>
        </div>

        {/* 底部详细参数面板 */}
        <div className="absolute -bottom-16 left-0 w-full flex justify-between px-2 font-mono text-[9px] text-white/20 tracking-widest uppercase">
          <div className="flex flex-col gap-1">
            <div className="flex items-center gap-2">
              <span className={isCritical ? 'text-red-600' : ''}>Sync_Volt:</span>
              <span>{(progress * 0.48).toFixed(2)}v</span>
            </div>
            <div>Module: AX-900_Core</div>
          </div>
          <div className="flex flex-col items-end gap-1">
            <div className="flex items-center gap-2">
              <span>Heat_Sign:</span>
              <span className={isCritical ? 'text-red-500 font-bold' : ''}>{Math.round(35 + (100 - progress) * 0.6)}°C</span>
            </div>
            <div className="flex gap-1.5 mt-1">
              {[1, 2, 3].map(i => (
                <div key={i} className={`w-1.5 h-1.5 rounded-full ${isCritical ? 'bg-red-600 animate-pulse' : 'bg-white/5'}`}></div>
              ))}
            </div>
          </div>
        </div>
      </button>

      {/* 底部工业装饰背景 */}
      <div className="mt-44 opacity-5 pointer-events-none select-none scale-125">
        <svg width="400" height="30" viewBox="0 0 400 30">
          <path d="M0 15 H150 L170 5 H230 L250 15 H400" fill="none" stroke="white" strokeWidth="0.5" />
        </svg>
      </div>
    </div>
  );
};

export default App;
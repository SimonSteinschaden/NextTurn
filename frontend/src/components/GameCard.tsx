import type { BoardGame } from "../api/nextTurnApi";
import {
    motion,
    useMotionValue,
    useSpring,
    useTransform
} from "framer-motion";

type Props = {
    game: BoardGame;
    exitDirection: "left" | "right" | "up" | "down";
    onSwipe?: (direction: "left" | "right") => void;
};

function GameCard({ game, exitDirection, onSwipe }: Props) {
    const exitAnimation = {
        left: {
            opacity: 0,
            x: -450,
            rotate: -25
        },
        right: {
            opacity: 0,
            x: 450,
            rotate: 25
        },
        up: {
            opacity: 0,
            y: -450,
            scale: 1.04
        },
        down: {
            opacity: 0,
            y: 450,
            scale: 0.96
        }
    }[exitDirection];

    const x = useMotionValue(0);
    const springX = useSpring(x, {
        stiffness: 220,
        damping: 22
    });

    const rotate = useTransform(springX, [-300, 0, 300], [-12, 0, 12]);
    const likeOpacity = useTransform(springX, [90, 180], [0, 1]);
    const nopeOpacity = useTransform(springX, [-180, -90], [1, 0]);

    return (
        <motion.div
            style={{ x: springX, rotate }}
            initial={{ opacity: 0, y: 30 }}
            animate={{ opacity: 1, y: 0 }}
            exit={exitAnimation}
            transition={{ type: "spring", stiffness: 190, damping: 20 }}
            drag="x"
            dragConstraints={{ left: -300, right: 300 }}
            dragElastic={0.8}
            dragMomentum={false}
            onDragEnd={(_, info) => {
                if (info.offset.x > 90) {
                    onSwipe?.("right");
                } else if (info.offset.x < -90) {
                    onSwipe?.("left");
                }
            }}
            whileDrag={{
                scale: 1.03
            }}
            className="relative bg-zinc-900 text-white rounded-3xl overflow-hidden shadow-2xl h-[720px] flex flex-col"
        >
            <motion.div
                style={{ opacity: likeOpacity }}
                className="absolute top-8 left-8 z-20 border-4 border-emerald-500 text-emerald-500 px-4 py-2 rounded-xl text-3xl font-black -rotate-12 bg-white/80"
            >
                LIKE
            </motion.div>

            <motion.div
                style={{ opacity: nopeOpacity }}
                className="absolute top-8 right-8 z-20 border-4 border-red-500 text-red-500 px-4 py-2 rounded-xl text-3xl font-black rotate-12 bg-white/80"
            >
                NOPE
            </motion.div>

            <img
                src={game.image || game.thumbnail}
                alt={game.name}
                draggable={false}
                className="w-full h-[320px] object-cover bg-zinc-800 flex-shrink-0"
            />

            <div className="p-6 flex-1 overflow-hidden flex flex-col">
                <div className="flex justify-between items-start gap-4">
                    <div className="min-w-0">
                        <h2 className="text-3xl font-bold leading-tight text-white">
                            {game.name}
                        </h2>
                        <p className="text-zinc-400 mt-2 text-sm">
                            {game.yearPublished}
                        </p>
                    </div>

                    <div className="bg-yellow-400 text-black px-3 py-1 rounded-full font-bold text-sm">
                        #{game.bggRank}
                    </div>
                </div>

                <div className="flex flex-wrap gap-3 mt-4 text-sm text-zinc-300">
                    <div>👥 {game.minPlayers}-{game.maxPlayers}</div>
                    <div>⭐ {game.averageRating?.toFixed(1)}</div>
                    <div>🧠 {game.complexity?.toFixed(1)}</div>
                </div>

                <div className="mt-6">
                    <h3 className="text-sm uppercase tracking-wider text-zinc-400 mb-2">
                        Categories
                    </h3>
                    <div className="flex flex-wrap gap-2">
                        {game.categories.map((category) => (
                            <span
                                key={category}
                                className="bg-blue-600 px-3 py-1 rounded-full text-sm"
                            >
                                {category}
                            </span>
                        ))}
                    </div>
                </div>

                <div className="mt-6">
                    <h3 className="text-sm uppercase tracking-wider text-zinc-400 mb-2">
                        Mechanics
                    </h3>
                    <div className="flex flex-wrap gap-2 overflow-y-auto max-h-[140px] pr-2">
                        {game.mechanics.slice(0, 8).map((mechanic) => (
                            <span
                                key={mechanic}
                                className="bg-zinc-700 px-3 py-1 rounded-full text-sm"
                            >
                                {mechanic}
                            </span>
                        ))}
                    </div>
                </div>
            </div>
        </motion.div>
    );
}

export default GameCard;

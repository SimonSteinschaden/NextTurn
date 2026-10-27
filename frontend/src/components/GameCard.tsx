import PlayerRecommendation from "./PlayerRecommendation";
import DesignerLine from "./DesignerLine";
import MatchQualityBadge from "./MatchQualityBadge";
import type { BoardGame } from "../api/nextTurnApi";
import { motion, useMotionValue, useTransform } from "framer-motion";
type Direction = "left" | "right" | "up" | "down" | "none";
type Props = {
  game: BoardGame;
  selectedPlayers?: number;
  exitDirection: Direction;
  onSwipe?: (direction: "left" | "right") => void;
  disabled?: boolean;
  planning?: boolean;
  onDetails?: () => void;
};
const exits = {
  none: { opacity: 0, x: 0, y: 0, rotate: 0, scale: 1 },
  left: { opacity: 0, x: -450, rotate: -25 },
  right: { opacity: 0, x: 450, rotate: 25 },
  up: { opacity: 0, y: -450, scale: 1.04 },
  down: { opacity: 0, y: 450, scale: 0.96 },
};
export default function GameCard({
  game,
  selectedPlayers,
  exitDirection,
  onSwipe,
  disabled = false,
  planning = false,
  onDetails,
}: Props) {
  const x = useMotionValue(0);
  const rotate = useTransform(x, [-300, 0, 300], [-12, 0, 12]);
  const likeOpacity = useTransform(x, [90, 180], [0, 1]);
  const nopeOpacity = useTransform(x, [-180, -90], [1, 0]);
  return (
    <motion.article
      className="swipe-game-card"
      style={{ x, rotate, touchAction: "pan-y" }}
      custom={exitDirection}
      variants={{ exit: (direction: Direction) => exits[direction] }}
      initial={{ opacity: 0, y: 25 }}
      animate={{ opacity: 1, y: 0 }}
      exit="exit"
      transition={{ type: "spring", stiffness: 190, damping: 22 }}
      drag={disabled ? false : "x"}
      dragConstraints={{ left: 0, right: 0 }}
      dragElastic={1}
      dragSnapToOrigin
      onDragEnd={(_, info) => {
        if (disabled) return;
        if (info.offset.x > 90) onSwipe?.("right");
        else if (info.offset.x < -90) onSwipe?.("left");
      }}
      whileDrag={{ scale: 1.02 }}
    >
      <div className="swipe-cover">
        {game.image || game.thumbnail ? (
          <img
            src={game.image || game.thumbnail}
            alt={game.name}
            draggable={false}
          />
        ) : (
          <span>♟</span>
        )}
        <div className="cover-shade" />
        <span className="swipe-rank">BGG #{game.bggRank ?? "–"}</span>
        <MatchQualityBadge quality={game.matchQuality} />
        <motion.div
          style={{ opacity: likeOpacity }}
          className="swipe-stamp like-stamp"
        >
          {planning ? "VORMERKEN" : "LIKE"}
        </motion.div>
        <motion.div
          style={{ opacity: nopeOpacity }}
          className="swipe-stamp nope-stamp"
        >
          {planning ? "HEUTE NICHT" : "NOPE"}
        </motion.div>
      </div>
      <div className="swipe-game-info">
        <p className="game-year">{game.yearPublished ?? "Jahr unbekannt"}</p>
        <h2>{game.name}</h2>
        <DesignerLine designers={game.designers} />
        <PlayerRecommendation game={game} selectedPlayers={selectedPlayers} />
        <div className="swipe-facts">
          <span>
            ♟ {game.minPlayers ?? "?"}–{game.maxPlayers ?? "?"}
          </span>
          <span>◷ {game.maxPlayTime ?? game.playingTime ?? "?"} Min.</span>
          <span>★ {game.averageRating?.toFixed(1) ?? "–"}</span>
          <span>◈ {game.complexity?.toFixed(1) ?? "–"}/5</span>
        </div>
        <p className="tag-heading">GENRES & THEMEN</p>
        <div className="swipe-tags">
          {game.categories.slice(0, 4).map((c) => (
            <span key={c}>{c}</span>
          ))}
        </div>
        <p className="tag-heading">MECHANIKEN</p>
        <div className="swipe-tags mechanics">
          {game.mechanics.slice(0, 4).map((m) => (
            <span key={m}>{m}</span>
          ))}
        </div>
        <button className="game-details-button" onClick={onDetails}>
          Spieldetails ansehen <span>↗</span>
        </button>
      </div>
    </motion.article>
  );
}

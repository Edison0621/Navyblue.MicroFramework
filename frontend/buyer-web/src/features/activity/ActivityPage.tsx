import { useEffect, useState } from 'react'
import { api } from '../../lib/api'
import type { FavoriteItem, FootprintItem } from '../../types'

export function ActivityPage() {
  const [favorites, setFavorites] = useState<FavoriteItem[]>([])
  const [footprints, setFootprints] = useState<FootprintItem[]>([])
  const [searches, setSearches] = useState<string[]>([])

  useEffect(() => {
    const run = async () => {
      setFavorites(await api.listFavorites())
      setFootprints(await api.listFootprints())
      setSearches(await api.listSearches())
    }
    void run()
  }, [])

  return (
    <section>
      <h2>行为中心</h2>
      <div className="card">
        <h3>收藏夹</h3>
        {favorites.length === 0 ? <p>暂无收藏</p> : favorites.map((x) => <p key={`${x.type}-${x.targetId}`}>{x.name}</p>)}
      </div>
      <div className="card">
        <h3>浏览足迹</h3>
        {footprints.length === 0 ? <p>暂无足迹</p> : footprints.map((x) => <p key={x.id}>{x.name}</p>)}
      </div>
      <div className="card">
        <h3>最近搜索</h3>
        {searches.length === 0 ? <p>暂无搜索记录</p> : searches.map((x) => <p key={x}>{x}</p>)}
      </div>
    </section>
  )
}

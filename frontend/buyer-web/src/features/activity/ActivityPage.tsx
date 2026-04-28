import { useEffect, useState } from 'react'
import { api } from '../../lib/api'
import type { FavoriteItem, FootprintItem } from '../../types'
import { EmptyState, PageHeader, SurfaceCard } from '../../shared/ui/Storefront'

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
      <PageHeader title="行为中心" subtitle="收藏、足迹、搜索记录统一查看" />
      <SurfaceCard>
        <h3>收藏夹</h3>
        {favorites.length === 0 ? <EmptyState title="暂无收藏" description="看到喜欢的商品，记得点击收藏" /> : favorites.map((x) => <p key={`${x.type}-${x.targetId}`}>{x.name}</p>)}
      </SurfaceCard>
      <SurfaceCard>
        <h3>浏览足迹</h3>
        {footprints.length === 0 ? <EmptyState title="暂无足迹" description="浏览过的商品会在这里展示" /> : footprints.map((x) => <p key={x.id}>{x.name}</p>)}
      </SurfaceCard>
      <SurfaceCard>
        <h3>最近搜索</h3>
        {searches.length === 0 ? <EmptyState title="暂无搜索记录" description="在搜索框输入关键词即可沉淀记录" /> : searches.map((x) => <p key={x}>{x}</p>)}
      </SurfaceCard>
    </section>
  )
}

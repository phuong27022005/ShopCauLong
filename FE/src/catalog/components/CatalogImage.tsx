import { useState } from 'react'
import { catalogImageUrl } from '../api'
import placeholder from '../../assets/racket-placeholder.svg'

interface CatalogImageProps {
  src: string | null
  alt: string
  className: string
}

export function CatalogImage({ src, alt, className }: CatalogImageProps) {
  const [failedSrc, setFailedSrc] = useState<string | null>(null)
  const candidate = src ? catalogImageUrl(src) : null
  const image = candidate && candidate !== failedSrc ? candidate : placeholder

  return (
    <img
      src={image}
      alt={alt}
      loading="lazy"
      decoding="async"
      className={className}
      onError={image !== placeholder ? () => setFailedSrc(candidate) : undefined}
    />
  )
}

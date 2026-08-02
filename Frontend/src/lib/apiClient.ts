type ApiClientOptions = {
  onUnauthorized?: () => void
}

type JsonValue = Record<string, unknown> | Array<unknown> | string | number | boolean | null

export function createApiClient(options: ApiClientOptions = {}) {
  const request = async (path: string, init?: RequestInit) => {
    const response = await fetch(path, {
      credentials: 'include',
      ...init
    })

    if (response.status === 401) {
      options.onUnauthorized?.()
    }

    return response
  }

  const get = (path: string) => request(path)

  const post = (path: string, body?: JsonValue) =>
    request(path, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: body === undefined ? undefined : JSON.stringify(body)
    })

  const del = (path: string) =>
    request(path, {
      method: 'DELETE'
    })

  return { get, post, del, request }
}
import { beforeEach, describe, expect, it } from 'vitest'
import { assertJourneyNames, findUnnamedClickables } from '../src/testing'

describe('findUnnamedClickables', () => {
  beforeEach(() => {
    document.body.innerHTML = ''
  })

  it('finds clickable elements without data-journey', () => {
    document.body.innerHTML = `
      <button data-journey="save">Save</button>
      <button id="cancel">Cancel</button>
      <a href="/help">Help</a>
      <a>Not a link without href</a>
      <div role="button">Menu</div>
      <p>Text</p>`
    expect(findUnnamedClickables(document.body).map((e) => e.textContent)).toEqual(['Cancel', 'Help', 'Menu'])
  })

  it('accepts elements inside a named container or data-journey-ignore', () => {
    document.body.innerHTML = `
      <nav data-journey="main-nav"><a href="/a">A</a></nav>
      <div data-journey-ignore><button>Next</button></div>`
    expect(findUnnamedClickables(document.body)).toEqual([])
  })

  it('accepts auto-nameable elements only when autoClickNames is set', () => {
    document.body.innerHTML = '<button data-testid="save">Save</button><button id="_r_2_">Go</button>'
    expect(findUnnamedClickables(document.body)).toHaveLength(2)
    expect(findUnnamedClickables(document.body, { autoClickNames: true }).map((e) => e.textContent)).toEqual(['Go'])
  })

  it('checks the root element itself', () => {
    document.body.innerHTML = '<button>Alone</button>'
    expect(findUnnamedClickables(document.querySelector('button')!)).toHaveLength(1)
  })

  it('honours custom attribute names', () => {
    document.body.innerHTML = '<button data-track="save">Save</button><button data-skip>Close</button>'
    expect(findUnnamedClickables(document.body, { clickAttribute: 'data-track', ignoreAttribute: 'data-skip' })).toEqual([])
  })
})

describe('assertJourneyNames', () => {
  it('passes when every clickable element is named', () => {
    document.body.innerHTML = '<button data-journey="save">Save</button>'
    expect(() => assertJourneyNames(document.body)).not.toThrow()
  })

  it('lists the opening tag of each unnamed element', () => {
    document.body.innerHTML = '<button class="danger">Delete</button><a href="/x">X</a>'
    expect(() => assertJourneyNames(document.body)).toThrowError(
      /2 clickable elements have no data-journey name[\s\S]*<button class="danger">[\s\S]*<a href="\/x">/,
    )
  })
})

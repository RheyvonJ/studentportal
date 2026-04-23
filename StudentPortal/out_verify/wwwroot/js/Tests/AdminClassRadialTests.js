// Simple test harness for AdminClass radial outside-click behavior
// Usage: call AdminClassRadialTests.runAll() from console after page loads
(function (global) {
  if (!global || !global.__AdminClassAPI) return;
  const api = global.__AdminClassAPI;

  function click(target) {
    const ev = new MouseEvent('click', { bubbles: true, cancelable: true, view: window });
    target.dispatchEvent(ev);
  }

  function ensureCard() {
    let card = document.querySelector('.content-card');
    if (!card) {
      card = document.createElement('article');
      card.className = 'content-card material';
      const t = document.createElement('div');
      t.className = 'content-title';
      t.textContent = 'Temp Content';
      card.appendChild(t);
      document.body.appendChild(card);
    }
    return card;
  }

  function testRadialStaysOpenDuringAction2OnCardClick() {
    api.setDeleteState();
    api.contentRadial.classList.add('show');
    api.addContentBtn.classList.add('selected');
    const card = ensureCard();
    click(card);
    const open = api.contentRadial.classList.contains('show');
    return { name: 'Action2 keeps radial open on card click', passed: open };
  }

  function testCollapseResumesAfterAction2Ends() {
    api.setDeleteState();
    api.setBaseState();
    click(document);
    const closed = !api.contentRadial.classList.contains('show');
    return { name: 'Collapse resumes after Action2 ends', passed: closed };
  }

  global.AdminClassRadialTests = {
    runAll() {
      const results = [
        testRadialStaysOpenDuringAction2OnCardClick(),
        testCollapseResumesAfterAction2Ends()
      ];
      console.table(results);
      return results;
    }
  };
})(window);

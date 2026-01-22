import { useState, useEffect, useRef } from 'react';
import { api, type Auction, type Transaction } from './api';
import './App.css';

// Таймер вынесен в отдельный компонент, чтобы не перерисовывать весь экран каждую секунду.
const CountDown = ({ targetDate }: { targetDate: string }) => {
  const [timeLeft, setTimeLeft] = useState('');
  const [isEnded, setIsEnded] = useState(false);

  useEffect(() => {
    const update = () => {
      const now = new Date().getTime();
      const end = new Date(targetDate).getTime();
      const diff = end - now;

      if (diff <= 0) {
        setTimeLeft('Finishing...');
        setIsEnded(true);
      } else {
        setIsEnded(false);
        const m = Math.floor((diff % (1000 * 60 * 60)) / (1000 * 60));
        const s = Math.floor((diff % (1000 * 60)) / 1000);
        setTimeLeft(`${m}:${s < 10 ? '0' + s : s}`);
      }
    };
    update();
    const interval = setInterval(update, 1000);
    return () => clearInterval(interval);
  }, [targetDate]);

  return <span className={isEnded ? 'timer-ending' : ''}>{timeLeft}</span>;
}

type Tab = 'ACTIVE' | 'INVENTORY' | 'CREATE' | 'HISTORY';

const Modal = ({ isOpen, onClose, title, children }: { 
  isOpen: boolean; 
  onClose: () => void; 
  title: string; 
  children: React.ReactNode 
}) => {
  if (!isOpen) return null;
  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-content" onClick={e => e.stopPropagation()}>
        <div className="modal-header">
          <h3>{title}</h3>
          <button className="modal-close" onClick={onClose}>×</button>
        </div>
        <div className="modal-body">
          {children}
        </div>
      </div>
    </div>
  );
};

function App() {
  const [user, setUser] = useState<{ _id: string; username: string; balance: number } | null>(null);
  const [usernameInput, setUsernameInput] = useState('');
  const [activeTab, setActiveTab] = useState<Tab>('ACTIVE');
  const [depositModalOpen, setDepositModalOpen] = useState(false);
  const [withdrawModalOpen, setWithdrawModalOpen] = useState(false);
  const [modalAmount, setModalAmount] = useState('');
  const [transferModalOpen, setTransferModalOpen] = useState(false);
  const [transferBidId, setTransferBidId] = useState('');
  const [transferRecipient, setTransferRecipient] = useState('');
  const [transferError, setTransferError] = useState('');
  const [auctions, setAuctions] = useState<Auction[]>([]);
  const [inventory, setInventory] = useState<any[]>([]);
  const [history, setHistory] = useState<Transaction[]>([]);
  const [myBids, setMyBids] = useState<Record<string, number>>({});
  const [showVictory, setShowVictory] = useState<{ title: string; fromUsername?: string } | null>(null);
  const [newAuctionTitle, setNewAuctionTitle] = useState('');
  const [selectedAuction, setSelectedAuction] = useState<Auction | null>(null);
  const [bidAmount, setBidAmount] = useState<number>(0);
  const [msg, setMsg] = useState('');

  useEffect(() => {
    // Восстановление сессии между перезагрузками страницы.
    const savedId = localStorage.getItem('userId');
    const savedName = localStorage.getItem('username');
    if (savedId && savedName) {
      api.getMe(savedId).then(u => setUser(u)).catch(() => localStorage.clear());
    }
  }, []);

  const handleLogin = async () => {
    try {
      const u = await api.login(usernameInput);
      setUser(u);
      localStorage.setItem('userId', u._id);
      localStorage.setItem('username', u.username);
      await loadAuctions(u._id);
    } catch (e: any) {
      setMsg('Login failed');
    }
  };

  const handleLogout = () => {
    localStorage.clear();
    setUser(null);
    setSelectedAuction(null);
    setUsernameInput('');
    setNewAuctionTitle('');
  }

  const loadAuctions = async (userId?: string) => {
    try {
      const list = await api.getAuctions();
      setAuctions(list);

      const uid = userId || user?._id;
      if (uid) {
        const bids = await api.getMyBids(uid);
        setMyBids(bids);
      }
    } catch (e) {
      console.error(e);
    }
  };

  const prevInventoryCountRef = useRef(0);
  const lastShownVictoryBidIdRef = useRef<string | null>(null);
  const seenInventoryBidIdsRef = useRef<Set<string>>(new Set());
  const inventoryInitializedRef = useRef(false);

  const loadInventory = async () => {
    if (!user) return;
    try {
      const items: any[] = await api.getInventory(user._id);

      items.sort((a: any, b: any) => {
        const at = new Date(a.date).getTime();
        const bt = new Date(b.date).getTime();
        return bt - at;
      });

      // Победа показывается один раз на конкретный выигрыш (bidId), чтобы не повторять анимацию при частом polling.
      const seen = seenInventoryBidIdsRef.current;

      if (!inventoryInitializedRef.current) {
        for (const it of items) {
          if (it?.bidId) seen.add(String(it.bidId));
        }
        inventoryInitializedRef.current = true;
        prevInventoryCountRef.current = items.length;
        setInventory(items);
        return;
      }

      let newlySeen: any | null = null;
      for (const it of items) {
        const bidId = it?.bidId ? String(it.bidId) : '';
        if (bidId && !seen.has(bidId)) {
          newlySeen = it;
          break;
        }
      }

      if (newlySeen) {
        const bidId = String(newlySeen.bidId);
        seen.add(bidId);

        if (lastShownVictoryBidIdRef.current !== bidId) {
          lastShownVictoryBidIdRef.current = bidId;
          setShowVictory({
            title: newlySeen.auction?.title || 'Gift',
            fromUsername: newlySeen.receivedFromUsername
          });
        }
      }

      for (const it of items) {
        if (it?.bidId) seen.add(String(it.bidId));
      }

      prevInventoryCountRef.current = items.length;
      setInventory(items);
    } catch (e) {
      console.error(e);
    }
  };

  const loadHistory = async () => {
    if (!user) return;
    try {
      const list = await api.getTransactions(user._id);
      setHistory(list);
    } catch (e) {
      console.error(e);
    }
  };

  const refreshUser = async () => {
    if (!user) return;
    const u = await api.getMe(user._id);
    setUser(u);
  };

  const viewAuction = async (id: string, preserveInput = false) => {
    try {
      const data = await api.getAuctionDetails(id);
      const prevRoundIndex = selectedAuction?.currentRoundIndex;
      setSelectedAuction(data);

      const currentRound = data.rounds[data.currentRoundIndex];
      const roundChanged = prevRoundIndex !== undefined && prevRoundIndex !== data.currentRoundIndex;

      if (currentRound && (!preserveInput || roundChanged)) {
        setBidAmount(currentRound.minBid);
      }
    } catch (e: any) {
      if (e.response?.status === 404) {
        setSelectedAuction(null);
        setMsg('Auction finished!');
        loadAuctions();
        if (activeTab === 'INVENTORY') loadInventory();
      } else {
        console.error('Error fetching auction:', e);
      }
    }
  };

  const handleBid = async () => {
    if (!user || !selectedAuction) return;
    try {
      await api.placeBid(selectedAuction._id, Number(bidAmount), user._id);
      setMsg('Bid placed!');
      refreshUser();
      viewAuction(selectedAuction._id, false);
      loadAuctions(user._id);

      setMyBids(prev => ({
        ...prev,
        [selectedAuction._id]: Number(bidAmount)
      }));
    } catch (e: any) {
      setMsg(e.response?.data?.error || e.message);
    }
  };



  useEffect(() => {
    if (!user) return;
    // Единый таймер обновляет данные, чтобы интерфейс оставался «живым» без ручного обновления.
    const interval = setInterval(() => {
      if (selectedAuction) {
        refreshUser();
        viewAuction(selectedAuction._id, true);
        return;
      }

      if (activeTab === 'ACTIVE') {
        loadAuctions(user._id);
        return;
      }

      if (activeTab === 'INVENTORY') {
        // Инвентарь обновляется отдельным фоновым polling (см. отдельный useEffect ниже)
        return;
      }

      if (activeTab === 'HISTORY') {
        loadHistory();
        return;
      }

      // CREATE: фоновое обновление не требуется
    }, 2000);
    return () => clearInterval(interval);
  }, [user, selectedAuction, activeTab]);

  useEffect(() => {
    if (!user) return;
    // Фоновая проверка инвентаря для анимации победы (не привязана к вкладке).
    const interval = setInterval(() => {
      loadInventory();
    }, 2000);
    return () => clearInterval(interval);
  }, [user]);

  useEffect(() => {
    if (activeTab === 'ACTIVE') loadAuctions();
    if (activeTab === 'INVENTORY') loadInventory();
    if (activeTab === 'HISTORY') loadHistory();
  }, [activeTab]);


  if (!user) {
    return (
      <div className="container center">
        <h1>TG-GIFTSAUCTION</h1>
        <div className="login-card">
          <h2 className="text-center">Login</h2>
          <input
            value={usernameInput}
            onChange={e => setUsernameInput(e.target.value)}
            placeholder="Username"
          />
          <button onClick={handleLogin}>Enter</button>
          {msg && <p className="error-msg">{msg}</p>}
        </div>
      </div>
    );
  }

  return (
    <div className="app-container">
      <header>
        <div className="logo"><img src="/favicon.png" alt="logo" className="logo-icon" /> TG-GIFTSAUCTION</div>
        <div className="user-panel">
          <span className="balance">{user.username} | <strong>{user.balance} Stars</strong></span>
          <div className="balance-actions">
            <button className="deposit-btn" title="Add Funds" onClick={() => {
              setModalAmount('');
              setDepositModalOpen(true);
            }}>+</button>
            <button className="withdraw-btn" title="Withdraw Funds" onClick={() => {
              setModalAmount('');
              setWithdrawModalOpen(true);
            }}>-</button>
            <button className="history-btn-icon" title="Transaction History" onClick={() => { setSelectedAuction(null); setActiveTab('HISTORY'); }}>
              <img src="/history.png" alt="history" />
            </button>
            <button className="exit-btn" onClick={handleLogout}>Exit</button>
          </div>
        </div>
      </header>

      <nav className="tabs">
        <button className={activeTab === 'ACTIVE' ? 'active' : ''} onClick={() => { setSelectedAuction(null); setActiveTab('ACTIVE'); }}>Active Auctions</button>
        <button className={activeTab === 'INVENTORY' ? 'active' : ''} onClick={() => { setSelectedAuction(null); setActiveTab('INVENTORY'); }}>My Inventory</button>

        <button className={activeTab === 'CREATE' ? 'active' : ''} onClick={() => { setSelectedAuction(null); setActiveTab('CREATE'); }}>Create Auction</button>
      </nav>

      <main>
        {msg && <div className="msg" style={{ marginBottom: 10 }}>{msg}</div>}

        {selectedAuction ? (
          <div className="auction-detail">
            <button onClick={() => {
              setSelectedAuction(null);
              loadAuctions(user!._id);
            }}>← Back</button>
            <h2 className="text-center">{selectedAuction.title}</h2>
            <div className="round-info">
              <h3>Round {selectedAuction.currentRoundIndex + 1} / {selectedAuction.rounds.length}</h3>
              {selectedAuction.rounds[selectedAuction.currentRoundIndex] && (
                <div className="timer">
                  {selectedAuction.rounds[selectedAuction.currentRoundIndex].endTime ?
                    <CountDown targetDate={selectedAuction.rounds[selectedAuction.currentRoundIndex].endTime!} /> :
                    <span>Waiting for first bid...</span>
                  }
                </div>
              )}
            </div>

            <div className="bidding-zone">
              <p className="text-center">Min Bid: {selectedAuction.rounds[selectedAuction.currentRoundIndex]?.minBid}</p>
              <input
                type="number"
                value={bidAmount}
                onChange={e => setBidAmount(Number(e.target.value))}
              />
              <button className="primary-btn" onClick={handleBid}>Place Bid</button>
            </div>

            <div className="leaderboard">
              <h3 className="text-center">Top Bids (Winners Zone)</h3>
              {selectedAuction.topBids
                ?.slice(0, selectedAuction.rounds[selectedAuction.currentRoundIndex].winnersCount)
                .map((b, i) => (
                  <div key={b._id} className={`bid-row rank-${i + 1}`}>
                    <span>
                      {i === 0 && '🥇 '}
                      {i === 1 && '🥈 '}
                      {i === 2 && '🥉 '}
                      #{i + 1} {(b.userId as any).username}
                    </span>
                    <span>{b.amount} Stars</span>
                  </div>
                ))}
              {(!selectedAuction.topBids || selectedAuction.topBids.length === 0) && <p className="text-center">No bids yet</p>}

              {(selectedAuction.topBids?.length || 0) > selectedAuction.rounds[selectedAuction.currentRoundIndex].winnersCount && (
                <p className="text-center" style={{ opacity: 0.5, marginTop: 10 }}>
                  + {(selectedAuction.topBids?.length || 0) - selectedAuction.rounds[selectedAuction.currentRoundIndex].winnersCount} other anonymous bids
                </p>
              )}
            </div>
          </div>
        ) : (
          <>
            {activeTab === 'ACTIVE' && (
              <div className="auction-list slide-enter">
                <h2 className="text-center">Active Auctions</h2>
                {auctions.map(a => (
                  <div key={a._id} className={`auction-card ${myBids[a._id] ? 'participating' : ''}`}>
                    <h3>{a.title}</h3>
                    <p>Round {a.currentRoundIndex + 1}</p>
                    <button onClick={() => viewAuction(a._id)}>Participate</button>
                  </div>
                ))}
                {auctions.length === 0 && <p className="text-center">No active auctions.</p>}
              </div>
            )}

            {activeTab === 'INVENTORY' && (
              <div className="inventory-list slide-enter">
                <h2 className="text-center">My Gifts</h2>
                <p className="text-center" style={{ opacity: 0.6, fontSize: '0.9rem', marginTop: -10 }}>Click on a gift to transfer it</p>
                <div className="inventory-grid">
                  {inventory.map((item, i) => (
                    <div 
                      key={i} 
                      className="inventory-item clickable"
                      onClick={() => {
                        setTransferBidId(item.bidId);
                        setTransferRecipient('');
                        setTransferError('');
                        setTransferModalOpen(true);
                      }}
                    >
                      <div className="gift-icon">🎁</div>
                      <h3>{item.auction?.title || 'Unknown Gift'}</h3>
                      <p>Won for: {item.amount} Stars</p>
                      <p className="winner-info">Winner: {item.winnerUsername}</p>
                      <span className="date">{new Date(item.date).toLocaleDateString()}</span>
                    </div>
                  ))}
                </div>
                {inventory.length === 0 && <p className="text-center">You haven't won any gifts yet.</p>}
              </div>
            )}

            {activeTab === 'HISTORY' && (
              <div className="history-list slide-enter">
                <h2 className="text-center">Transaction History</h2>
                <div className="history-container">
                  <table className="history-table">
                    <thead>
                      <tr>
                        <th>Type</th>
                        <th>Amount</th>
                        <th>Date</th>
                      </tr>
                    </thead>
                    <tbody>
                      {history.map(tx => (
                        <tr key={tx._id}>
                          <td>
                            <span className="badge">{tx.type}</span>
                          </td>
                          <td style={{
                            fontWeight: 'bold',
                            color: tx.amount > 0 ? '#4ade80' : '#f87171'
                          }}>
                            {tx.amount > 0 ? '+' : ''}{tx.amount}
                          </td>
                          <td>{new Date(tx.createdAt).toLocaleString()}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
                {history.length === 0 && <p className="text-center">No transactions found.</p>}
              </div>
            )}

            {activeTab === 'CREATE' && (
              <div className="create-container slide-enter">
                <h2 className="text-center">Create New Auction</h2>
                <div className="card create-form">
                  <div className="form-group">
                    <label>Gift Name</label>
                    <input value={newAuctionTitle} onChange={e => setNewAuctionTitle(e.target.value)} placeholder="E.g. Rare Username" />
                  </div>

                  <div className="form-row">
                    <div className="form-group">
                      <label>Min Bid</label>
                      <input type="number" defaultValue={10} id="newMinBid" />
                    </div>
                    <div className="form-group">
                      <label>Winners Count</label>
                      <input type="number" defaultValue={1} id="newWinnersCount" />
                    </div>
                  </div>

                  <div className="form-group">
                    <label>Rounds Count</label>
                    <input type="number" defaultValue={1} min={1} max={1000} id="newRoundsCount" />
                  </div>

                  <div className="form-group center">
                    <label>Round Duration</label>
                    <div className="duration-inputs">
                      <input 
                        type="number" 
                        defaultValue={1} 
                        min={0} 
                        max={59}
                        id="newDurationMin" 
                        placeholder="min"
                      />
                      <span>:</span>
                      <input 
                        type="number" 
                        defaultValue={0} 
                        min={0} 
                        max={59}
                        id="newDurationSec" 
                        placeholder="sec"
                      />
                    </div>
                  </div>

                  <br />
                  <button onClick={() => {
                    const minBid = Number((document.getElementById('newMinBid') as HTMLInputElement).value);
                    const winnersCount = Number((document.getElementById('newWinnersCount') as HTMLInputElement).value);
                    const roundsCount = Number((document.getElementById('newRoundsCount') as HTMLInputElement).value);
                    const mins = Math.max(0, Math.min(59, Number((document.getElementById('newDurationMin') as HTMLInputElement).value) || 0));
                    const secs = Math.max(0, Math.min(59, Number((document.getElementById('newDurationSec') as HTMLInputElement).value) || 0));
                    const durationMs = (mins * 60 + secs) * 1000;

                    if (durationMs < 30000) {
                      setMsg('Minimum duration is 0:30');
                      return;
                    }

                    if (!Number.isFinite(roundsCount) || roundsCount < 1 || roundsCount > 1000) {
                      setMsg('Rounds count must be between 1 and 1000');
                      return;
                    }

                    api.createAuction({
                      title: newAuctionTitle,
                      minBid,
                      winnersCount,
                      duration: durationMs,
                      roundsCount
                    }, user!._id).then(async () => {
                      setNewAuctionTitle('');
                      await loadAuctions();
                      setActiveTab('ACTIVE');
                      setMsg('Auction created!');
                      setTimeout(() => setMsg(''), 3000);
                    }).catch((e: any) => {
                      setMsg(e.response?.data?.error || e.message);
                    });

                  }}>Create Auction</button>
                </div>
              </div>
            )}
          </>
        )}
      </main>
      
      <Modal 
        isOpen={depositModalOpen} 
        onClose={() => setDepositModalOpen(false)} 
        title="Add Funds"
      >
        <input
          type="number"
          value={modalAmount}
          onChange={e => setModalAmount(e.target.value)}
          placeholder="Enter amount"
          autoFocus
        />
        <button 
          className="primary-btn"
          style={{ marginTop: 15, width: '100%' }}
          onClick={async () => {
            if (modalAmount && Number(modalAmount) > 0) {
              try {
                await api.deposit(Number(modalAmount), user!._id);
                refreshUser();
                setDepositModalOpen(false);
                setMsg('Deposit successful!');
                setTimeout(() => setMsg(''), 3000);
              } catch (e: any) { 
                setMsg(e.response?.data?.error || e.message);
              }
            }
          }}
        >
          Deposit
        </button>
      </Modal>

      <Modal 
        isOpen={withdrawModalOpen} 
        onClose={() => setWithdrawModalOpen(false)} 
        title="Withdraw Funds"
      >
        <input
          type="number"
          value={modalAmount}
          onChange={e => setModalAmount(e.target.value)}
          placeholder="Enter amount"
          autoFocus
        />
        <button 
          className="primary-btn"
          style={{ marginTop: 15, width: '100%' }}
          onClick={async () => {
            if (modalAmount && Number(modalAmount) > 0) {
              try {
                await api.deposit(-Number(modalAmount), user!._id);
                refreshUser();
                setWithdrawModalOpen(false);
                setMsg('Withdrawal successful!');
                setTimeout(() => setMsg(''), 3000);
              } catch (e: any) { 
                setMsg(e.response?.data?.error || e.message);
              }
            }
          }}
        >
          Withdraw
        </button>
      </Modal>

      <Modal 
        isOpen={transferModalOpen} 
        onClose={() => setTransferModalOpen(false)} 
        title="Transfer Gift"
      >
        <p style={{ marginBottom: 15, opacity: 0.8 }}>Enter the username of the recipient:</p>
        <input
          type="text"
          value={transferRecipient}
          onChange={e => setTransferRecipient(e.target.value)}
          placeholder="Username"
          autoFocus
        />
        {transferError && <p className="error-msg" style={{ marginTop: 10 }}>{transferError}</p>}
        <button 
          className="primary-btn"
          style={{ marginTop: 15, width: '100%' }}
          onClick={async () => {
            if (transferRecipient.trim()) {
              try {
                setTransferError('');
                await api.transferGift(transferBidId, transferRecipient.trim(), user!._id);
                setTransferModalOpen(false);
                loadInventory();
                setMsg('Gift transferred successfully!');
                setTimeout(() => setMsg(''), 3000);
              } catch (e: any) { 
                setTransferError(e.response?.data?.error || e.message);
              }
            }
          }}
        >
          Transfer
        </button>
      </Modal>

      {showVictory && (
        <div className="victory-overlay">
          <div className="victory-content">
            <div className="victory-icon">🎉</div>
            <h2>Congratulations!</h2>
            {showVictory.fromUsername ? (
              <p>{showVictory.fromUsername} sent you</p>
            ) : (
              <p>You won</p>
            )}
            <h3>{showVictory.title}</h3>
            <button onClick={() => setShowVictory(null)}>Ok</button>
          </div>
        </div>
      )}
    </div>
  );
}

export default App;

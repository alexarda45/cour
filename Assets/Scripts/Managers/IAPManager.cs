using UnityEngine;

#if CHROMA_USE_UNITY_IAP
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;
#endif

namespace ChromaBlast
{
    public class IAPManager : MonoBehaviour
#if CHROMA_USE_UNITY_IAP
        , IDetailedStoreListener
#endif
    {
        public static IAPManager Instance { get; private set; }

#pragma warning disable 0414
        [SerializeField] private string removeAdsProductId = "remove_ads";
        [SerializeField] private string neonCosmeticPackProductId = "neon_cosmetic_pack";
#pragma warning restore 0414

#if CHROMA_USE_UNITY_IAP
        private IStoreController controller;
        private IExtensionProvider extensions;
#endif

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            InitializePurchasing();
        }

        public void BuyRemoveAds()
        {
#if CHROMA_USE_UNITY_IAP
            if (controller != null)
            {
                controller.InitiatePurchase(removeAdsProductId);
            }
#elif UNITY_EDITOR
            Debug.Log("IAP simulat in Editor: Remove Ads.");
            CompleteRemoveAdsPurchase();
#else
            Debug.LogWarning("Unity IAP nu este activ. Instaleaza pachetul si adauga CHROMA_USE_UNITY_IAP.");
#endif
        }

        public void BuyCosmeticPack()
        {
#if CHROMA_USE_UNITY_IAP
            if (controller != null)
            {
                controller.InitiatePurchase(neonCosmeticPackProductId);
            }
#elif UNITY_EDITOR
            Debug.Log("IAP simulat in Editor: Cosmetic Pack.");
            CompleteCosmeticPackPurchase();
#else
            Debug.LogWarning("Unity IAP nu este activ. Instaleaza pachetul si adauga CHROMA_USE_UNITY_IAP.");
#endif
        }

        public void RestorePurchases()
        {
#if CHROMA_USE_UNITY_IAP
            extensions?.GetExtension<IAppleExtensions>()?.RestoreTransactions((success, message) =>
            {
                Debug.Log($"Restore purchases: {success} {message}");
            });
#endif
        }

        private void CompleteRemoveAdsPurchase()
        {
            SaveManager.Instance?.SetRemoveAds(true);
            AnalyticsManager.Instance?.RecordPurchase(removeAdsProductId);
        }

        private void CompleteCosmeticPackPurchase()
        {
            SaveManager.Instance?.SetCosmeticPackOwned(true);
            AnalyticsManager.Instance?.RecordPurchase(neonCosmeticPackProductId);
        }

        private void InitializePurchasing()
        {
#if CHROMA_USE_UNITY_IAP
            ConfigurationBuilder builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
            builder.AddProduct(removeAdsProductId, ProductType.NonConsumable);
            builder.AddProduct(neonCosmeticPackProductId, ProductType.NonConsumable);
            UnityPurchasing.Initialize(this, builder);
#endif
        }

#if CHROMA_USE_UNITY_IAP
        public void OnInitialized(IStoreController storeController, IExtensionProvider extensionProvider)
        {
            controller = storeController;
            extensions = extensionProvider;
        }

        public void OnInitializeFailed(InitializationFailureReason error)
        {
            Debug.LogWarning($"Unity IAP init failed: {error}");
        }

        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            Debug.LogWarning($"Unity IAP init failed: {error} - {message}");
        }

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
        {
            if (args.purchasedProduct.definition.id == removeAdsProductId)
            {
                CompleteRemoveAdsPurchase();
            }
            else if (args.purchasedProduct.definition.id == neonCosmeticPackProductId)
            {
                CompleteCosmeticPackPurchase();
            }

            return PurchaseProcessingResult.Complete;
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
        {
            Debug.LogWarning($"Purchase failed {product.definition.id}: {failureReason}");
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureDescription failureDescription)
        {
            Debug.LogWarning($"Purchase failed {product.definition.id}: {failureDescription.reason} {failureDescription.message}");
        }
#endif
    }
}

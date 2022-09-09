using System;
using UnityEngine;
using Reflex.Injectors;
using System.Collections.Generic;

namespace Reflex.Scripts.Core
{
    public class ContainerStack : IContainer
    {
        private readonly Stack<Container> _stack = new Stack<Container>();

        private readonly GameObject _tempRoot;

        public ContainerStack()
        {
            _tempRoot = new GameObject("Container Stack");
            _tempRoot.SetActive(false);
        }

        internal Container PushNew()
        {
            var container = new Container();
            container.BindSingleton<IContainer>(this);
            _stack.Push(container);
            return container;
        }

        internal Container Pop()
        {
            return _stack.Pop();
        }

        public void Dispose()
        {
            while (_stack.TryPop(out var container))
            {
                container.Dispose();
            }
        }

        public void AddDisposable(IDisposable disposable)
        {
            _stack.Peek().AddDisposable(disposable);
        }

        public MonoBehaviour InjectMono(MonoBehaviour instance)
        {
            MonoInjector.Inject(instance, this);
            return instance;
        }

        public T Instantiate<T>(T original, Transform container = null) where T : Component
        {
            return Instantiate_Internal(original, container, (parent) => UnityEngine.Object.Instantiate<T>(original, parent));
        }

        public T Instantiate<T>(T original, Transform container, bool worldPositionStays) where T : Component
        {
            return Instantiate_Internal(original, container, (parent) => UnityEngine.Object.Instantiate<T>(original, parent, worldPositionStays));
        }

        public T Instantiate<T>(T original, Vector3 position, Quaternion rotation, Transform container = null) where T : Component
        {
            return Instantiate_Internal(original, container, (parent) => UnityEngine.Object.Instantiate<T>(original, position, rotation, parent));
        }

        public GameObject Instantiate(GameObject original)
        {
            var instance = UnityEngine.Object.Instantiate<GameObject>(original);
            instance.GetComponentsInChildren<MonoBehaviour>().ForEach(mb => MonoInjector.Inject(mb, this));
            return instance;
        }

        public T Construct<T>()
        {
            return ConstructorInjector.ConstructAndInject<T>(this);
        }

        public object Construct(Type concrete)
        {
            return ConstructorInjector.ConstructAndInject(concrete, this);
        }

        public BindingContractDefinition<TContract> Bind<TContract>()
        {
            return _stack.Peek().Bind<TContract>();
        }

        public BindingGenericContractDefinition BindGenericContract(Type genericContract)
        {
            return new BindingGenericContractDefinition(genericContract, _stack.Peek());
        }

        public TContract Resolve<TContract>()
        {
            return (TContract) Resolve(typeof(TContract));
        }

        public object Resolve(Type contract)
        {
            foreach (var container in _stack)
            {
                try
                {
                    var instance = container.Resolve(contract);
                    if (instance != null)
                    {
                        return instance;
                    }
                }
                catch (Exception)
                {
                }
            }

            throw new Exception("Could not resolve");
        }

        public TCast ResolveGenericContract<TCast>(Type genericContract, params Type[] genericConcrete)
        {
            var contract = genericContract.MakeGenericType(genericConcrete);
            return (TCast) Resolve(contract);
        }

        public void BindSingleton<TContract>(TContract instance)
        {
            _stack.Peek().BindSingleton<TContract>(instance);
        }

        private T Instantiate_Internal<T>(T original, Transform container, Func<Transform, T> instantiate) where T : Component
        {
            var parent = container;
            var prefabWasActive = original.gameObject.activeSelf;

            if (prefabWasActive)
                parent = _tempRoot.transform;

            var instance = instantiate.Invoke(parent);

            if (prefabWasActive)
                instance.gameObject.SetActive(false);

            if (instance.transform.parent != container)
                instance.transform.SetParent(container, false);

            InjectMonoBehaviour(instance);

            instance.gameObject.SetActive(prefabWasActive);

            return instance;
        }

        private void InjectMonoBehaviour<T>(T instance) where T : Component
        {
            instance.GetComponentsInChildren<MonoBehaviour>().ForEach(mb => MonoInjector.Inject(mb, this));
        }
    }
}
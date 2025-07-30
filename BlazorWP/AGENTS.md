Best Practice for Custom Field Update Logic with Binding in .NET 9 Blazor

Implementing Change Logic with Two-Way Binding in Blazor (.NET 9)
The @bind:after Modifier – Post-Change Logic in .NET 9
@bind:after in a nutshell: Introduced in .NET 7 and fully supported by .NET 9, the @bind:after directive allows you to attach custom logic that runs immediately after a two-way bound value is updated

devblogs.microsoft.com
devblogs.microsoft.com
. In practice, this means you can keep clean two-way data binding and still invoke a method (including asynchronous methods) when an input’s value changes. The bound field or property is updated first (synchronously), then your method is invoked, so your code can directly use the new value without needing to manually retrieve it from event args

mikesdotnetting.com
mikesdotnetting.com
. This approach is now the idiomatic way in Blazor to react to input changes, especially for triggering async operations (e.g. validations, searches) on value change
devblogs.microsoft.com
mikesdotnetting.com
. Stability and support: @bind:after is a first-class feature of ASP.NET Core – it was added to solve the lack of a “safe way” to run post-binding logic
github.com
. By .NET 9 it’s well-supported and documented by Microsoft as the preferred pattern. It is fully integrated into the Razor compiler and Blazor’s change detection lifecycle, so using it is not a hack but a recommended practice. (An early .NET 7 patch was required for full functionality, but in current .NET 9 any initial quirks have been resolved
mikesdotnetting.com
.) How to use it: Simply add @bind:after="YourMethod" alongside your bind. The method can return void or Task (for async). For example, to call an async search whenever a text box value changes, you can do:

```
<input @bind="searchText" @bind:after="PerformSearch" />
@code {
    private string searchText;
    private async Task PerformSearch() 
    {
        // Use the updated searchText here (already bound)
        results = await SearchService.QueryAsync(searchText);
    }
}
```
In this example, PerformSearch is automatically invoked after any change to searchText is detected
devblogs.microsoft.com
devblogs.microsoft.com
. By default, a plain <input> uses the onchange event (fires on blur or Enter) for two-way binding. If you want to react on every keystroke, you can combine @bind:event="oninput" with @bind:after to trigger on the input event instead

learn.microsoft.com
learn.microsoft.com
:
```
<input @bind="searchText" @bind:event="oninput" @bind:after="PerformSearch" />
```
Similarly, a <select> dropdown can use @bind:after to run logic after a selection is made. For example:
```
<select @bind="SelectedState" @bind:after="OnStateChanged">
    <option value="">-- choose state --</option>
    <option value="AK">Alaska</option>
    <option value="MT">Montana</option>
    <!-- ... -->
</select>
@code {
    private string? SelectedState;
    private void OnStateChanged()
    {
        // SelectedState has been updated; use it here
        //Console.WriteLine($"User chose: {SelectedState}");
    }
}
```
If the select’s bound value changes (e.g. user picks "Montana"), Blazor updates SelectedState and then calls OnStateChanged(). No manual event handler or value assignment is needed – two-way binding remains intact. (Ensure the method signature matches: no parameters, since @bind:after doesn’t pass an event arg. Use the bound property itself to get the value.)

Using @bind:after with Custom Components (e.g. TinyMCEEditor)
The @bind:after pattern works for both native elements and components. For built-in form components like <InputText>, you use the parameter name in the binding syntax. For example, to run logic after an <InputText> (which binds to a model property) changes:
```
<InputText @bind-Value="Account.Username" @bind-Value:after="CheckUsernameAsync" />
```
In this case, CheckUsernameAsync will execute after Account.Username is updated (whether on blur by default, or on each keystroke if combined with @bind-Value:event="oninput"). This approach was demonstrated for an EditForm scenario to perform async username checks without breaking two-way binding
mikesdotnetting.com
mikesdotnetting.com
. Because the framework handles updating the bound value and tracking field changes, your CheckUsernameAsync method can focus solely on the custom logic (e.g. calling an API and adding a validation message), making the code much cleaner
mikesdotnetting.com
. For third-party or custom components (like a TinyMCEEditor WYSIWYG), the same idea applies as long as the component is set up for two-way binding. Typically, such components have a Value parameter and a corresponding ValueChanged event callback. In .NET 9, you can bind to them with @bind-Value and use :after. For example, if TinyMCEEditor exposes a Value parameter:
```
<TinyMCEEditor @bind-Value="postContent" @bind-Value:after="OnContentChanged" ... />
@code {
    private string postContent;
    private void OnContentChanged()
    {
        // Logic to run after editor content changes, e.g. update preview or set dirty flag
        isDirty = true;
    }
}
```
This will keep postContent in sync with the editor text, and call OnContentChanged() after each update. You no longer need a separate OnContentChanged event callback on the component (some libraries provided such events to work around older limitations). Instead of handling a custom event and manually syncing the value, Blazor’s binding system does it for you. This means fewer opportunities for error and no duplicate update logic. (If the TinyMCE component didn’t originally support @bind-Value, ensure it has a [Parameter] for the content and an [Parameter] EventCallback for changes – but most integration libraries do. The example above assumes those exist, as the usage of @bind-Value suggests
tiny.cloud
.) Important: @bind:after expects a method delegate. You cannot directly assign an EventCallback or lambda property to it (the compiler will error). In other words, do not attempt @bind:after="MyEventCallback" – instead provide the method name (or an inline lambda/callback). The Blazor docs note that using an EventCallback<T> with @bind:after isn’t supported; you should pass a method returning void or Task instead
learn.microsoft.com
. In practice this is rarely a concern, since you typically call a local method. Just be aware you can’t point it at a parameter of type EventCallback – define a method to call instead.

Why @bind:after (and Friends) Are Preferred Over Older Patterns
Before @bind:after and related binding enhancements, developers had to use workarounds to run logic on value change. .NET 9+ renders these workarounds unnecessary – and using them can cause conflicts. Let’s compare approaches and why the modern pattern is better:

Manual event handlers (@onchange/@oninput): Attaching an explicit event handler to an element that is also two-way bound is not allowed. The Blazor compiler will throw an error RZ10008 if you try (e.g. <input @bind="Name" @onchange="OnNameChanged">), because the @bind already uses that event under the hood
mikesdotnetting.com
. In other words, you can’t have two onchange handlers on the same element – one is generated by @bind. The only way around that was to remove @bind (losing two-way sync) or use a different DOM event (like adding an @oninput alongside a bind that defaults to onchange). But if you go fully manual (one-way bind to value + @onchange), you then must write code to update the model and handle any formatting. This is error-prone and breaks Blazor’s built-in state consistency (for example, on Blazor Server, manually handling events can lead to dropped keystrokes under latency, because without @bind Blazor won’t enforce value consistency)

github.com
learn.microsoft.com
. In short, using raw events for two-way updates is “dysfunctional” and not truly two-way
learn.microsoft.com
learn.microsoft.com
. The official guidance is clear: avoid trying to implement two-way binding with event handlers
learn.microsoft.com
. Instead, use the binding modifiers that Blazor provides.

Property setter logic: A common older pattern was to bind to a C# property and put custom logic in its set accessor (e.g. trimming or validating input). This works for synchronous logic and was even an early recommendation for simple cases. However, property setters cannot be asynchronous (and doing long work in them can block rendering). If you needed to call an async method when a value changed, this approach hit a dead end
mikesdotnetting.com
. The workaround again was to drop two-way binding and handle events manually (with the downsides noted above)
mikesdotnetting.com
. In .NET 7+, there is a cleaner alternative: using @bind:get and @bind:set modifiers in your component or element binding to achieve the same as a custom getter/setter, but within the Razor markup. This new syntax essentially replaces the “property with logic” pattern in a safer way
devblogs.microsoft.com
learn.microsoft.com
. For example, rather than relying on a hidden C# setter to intercept changes, you can do:
```
<input @bind:get="valueProp" @bind:set="(val) => valueProp = DoTransform(val)" />
```
or in a component,
```
<MyInput @bind-Value:get="CurrentValue" @bind-Value:set="OnValueSet" />
```
The @bind:set method can contain your custom logic (even async if needed as a Task-returning method), giving you a supported way to filter or modify the incoming value. Use @bind:get/set if you need to validate/transform the value before it’s applied, and use @bind:after if you simply want to react after the value is set (without altering it). In general, hooking heavy logic in a property setter is no longer recommended now that these explicit binding hooks exist – they make the intent clearer and avoid weird side-effects.

Manual ValueChanged callbacks on components: For Blazor components that use a [Parameter] Value and [Parameter] EventCallback ValueChanged pair, you might consider subscribing to the ValueChanged event in your parent to run code. However, you cannot use both the @bind-Value directive and a ValueChanged attribute at the same time – doing so causes a compile error RZ10010 (“The component parameter 'ValueChanged' is used two or more times…")
stackoverflow.com
. This is because @bind-Value already wires up the ValueChanged internally, so you’re essentially attempting to hook it twice. The result is a conflict. The older workaround was to not use @bind-Value at all, and instead manually set Value=@model and ValueChanged="@((T newVal) => { model = newVal; ...custom logic... })". This achieves two-way binding in user code, but again is more verbose and error-prone (you must remember to assign the new value, or the UI and model will diverge). With @bind:after, there’s no need to do that. The best practice is to let @bind handle the value syncing, and use :after for any extra actions. This keeps your markup clean and avoids the duplicate-parameter problem entirely. In our TinyMCE example above, we used @bind-Value:after="OnContentChanged" instead of manually tying into a OnContentChanged event callback provided by the component. This way, we didn’t have to duplicate any value assignment logic – Blazor calls our method only after it has already applied the new content to postContent. The code is shorter and free of the common mistakes (and we dodge RZ10010).

Best-Practice Recommendation (Blazor .NET 9 and Beyond)
Use Blazor’s binding extension points rather than manual event handling for input change logic. In practice, this means favoring the @bind:after modifier to run post-update code and the @bind:get/@bind:set modifiers to intercept or transform values during the update, as needed. This unified pattern scales well to complex forms: you can mix and match these modifiers to handle virtually any scenario without breaking two-way binding or causing compiler errors. The approach keeps your components state in sync with the UI (leveraging Blazor’s internal change tracking) and avoids the common pitfalls that older techniques had (e.g. forgetting to update the bound value, or losing keystrokes in server-side Blazor due to unsynchronized manual updates
github.com
). By using @bind... directives exclusively, you ensure that only one source of truth is updating the data and DOM, which means no conflicts (thus no RZ10008/RZ10010 errors).

In summary, Blazor .NET 9’s idiomatic solution for “run logic on input change” is: bind your inputs for two-way sync, and attach your logic with @bind:after. This lets you react to changes cleanly and asynchronously, without sacrificing the convenience and consistency of two-way binding
mikesdotnetting.com
. Reserve @bind:get/set for cases where you need to manipulate the value as it’s being set (or when implementing your own bindable component parameters). By following this pattern, your form interaction code remains concise, declarative, and free of the common Razor binding errors – the framework handles the heavy lifting, and your custom logic cleanly plugs in at the appropriate point in the update cycle.

References: Blazor official documentation and community examples reinforce this guidance. Microsoft’s .NET docs explicitly advise against using event handlers for two-way binding and instead using the @bind modifiers
learn.microsoft.com
. The new @bind:after capability was introduced specifically to “solve the problem and keep two-way binding” when running additional logic on value change
mikesdotnetting.com
. Therefore, it is considered a stable and recommended approach in .NET 9. By adopting @bind:after for post-change logic on both HTML elements and components, you’ll write more maintainable form code and avoid the typical mistakes that lead to errors like RZ10008 or RZ10010
mikesdotnetting.com
stackoverflow.com
. This unified pattern scales from simple inputs to complex components and ensures your Blazor app’s forms remain clean, responsive, and error-free. Sources: Citations include the official ASP.NET Core Blazor documentation and .NET team blog (for feature details) as well as Stack Overflow and community articles illustrating older pitfalls and the newer solutions. These are provided inline for reference.

## WordPress API Integration

When building features that communicate with a WordPress site, use the
[WordPressPCL](https://github.com/wp-net/WordPressPCL) library for all API
interactions. This ensures compatibility with WordPress REST endpoints and
provides strongly typed models for posts, pages and other resources.

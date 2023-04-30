namespace rec Microsoft.AspNetCore.Components.DslInternals

open Fun.Blazor
open Fun.Blazor.Operators
open Microsoft.AspNetCore.Components.DslInternals

type ErrorBoundaryBaseBuilder<'FunBlazorGeneric when 'FunBlazorGeneric :> Microsoft.AspNetCore.Components.IComponent>() =
    inherit ComponentWithDomAndChildAttrBuilder<'FunBlazorGeneric>()
    [<CustomOperation("ErrorContent")>] member inline _.ErrorContent ([<InlineIfLambda>] render: AttrRenderFragment, fn: System.Exception -> NodeRenderFragment) = render ==> html.renderFragment("ErrorContent", fn)
    [<CustomOperation("MaximumErrorCount")>] member inline _.MaximumErrorCount ([<InlineIfLambda>] render: AttrRenderFragment, x: System.Int32) = render ==> ("MaximumErrorCount" => x)

            
namespace rec Microsoft.AspNetCore.Components.DslInternals.Web

open Microsoft.AspNetCore.Components.DslInternals

type ErrorBoundaryBuilder<'FunBlazorGeneric when 'FunBlazorGeneric :> Microsoft.AspNetCore.Components.IComponent>() =
    inherit ErrorBoundaryBaseBuilder<'FunBlazorGeneric>()


            
namespace rec Microsoft.AspNetCore.Components.DslInternals.Routing

open Fun.Blazor
open Fun.Blazor.Operators

type FocusOnNavigateBuilder<'FunBlazorGeneric when 'FunBlazorGeneric :> Microsoft.AspNetCore.Components.IComponent>() =
    inherit ComponentWithDomAndChildAttrBuilder<'FunBlazorGeneric>()
    [<CustomOperation("RouteData")>] member inline _.RouteData ([<InlineIfLambda>] render: AttrRenderFragment, x: Microsoft.AspNetCore.Components.RouteData) = render ==> ("RouteData" => x)
    [<CustomOperation("Selector")>] member inline _.Selector ([<InlineIfLambda>] render: AttrRenderFragment, x: System.String) = render ==> ("Selector" => x)

type NavLinkBuilder<'FunBlazorGeneric when 'FunBlazorGeneric :> Microsoft.AspNetCore.Components.IComponent>() =
    inherit ComponentWithDomAndChildAttrBuilder<'FunBlazorGeneric>()
    [<CustomOperation("ActiveClass")>] member inline _.ActiveClass ([<InlineIfLambda>] render: AttrRenderFragment, x: System.String) = render ==> ("ActiveClass" => x)
    [<CustomOperation("AdditionalAttributes")>] member inline _.AdditionalAttributes ([<InlineIfLambda>] render: AttrRenderFragment, x: System.Collections.Generic.IReadOnlyDictionary<System.String, System.Object>) = render ==> ("AdditionalAttributes" => x)
    [<CustomOperation("Match")>] member inline _.Match ([<InlineIfLambda>] render: AttrRenderFragment, x: Microsoft.AspNetCore.Components.Routing.NavLinkMatch) = render ==> ("Match" => x)

            
namespace rec Microsoft.AspNetCore.Components.DslInternals.Web

open Fun.Blazor

type HeadContentBuilder<'FunBlazorGeneric when 'FunBlazorGeneric :> Microsoft.AspNetCore.Components.IComponent>() =
    inherit ComponentWithDomAndChildAttrBuilder<'FunBlazorGeneric>()


type HeadOutletBuilder<'FunBlazorGeneric when 'FunBlazorGeneric :> Microsoft.AspNetCore.Components.IComponent>() =
    inherit ComponentWithDomAndChildAttrBuilder<'FunBlazorGeneric>()


type PageTitleBuilder<'FunBlazorGeneric when 'FunBlazorGeneric :> Microsoft.AspNetCore.Components.IComponent>() =
    inherit ComponentWithDomAndChildAttrBuilder<'FunBlazorGeneric>()


            
namespace rec Microsoft.AspNetCore.Components.DslInternals.Web.Virtualization

open Fun.Blazor
open Fun.Blazor.Operators

type VirtualizeBuilder<'FunBlazorGeneric, 'TItem when 'FunBlazorGeneric :> Microsoft.AspNetCore.Components.IComponent>() =
    inherit ComponentWithDomAndChildAttrBuilder<'FunBlazorGeneric>()
    [<CustomOperation("ChildContent")>] member inline _.ChildContent ([<InlineIfLambda>] render: AttrRenderFragment, fn: 'TItem -> NodeRenderFragment) = render ==> html.renderFragment("ChildContent", fn)
    [<CustomOperation("ItemContent")>] member inline _.ItemContent ([<InlineIfLambda>] render: AttrRenderFragment, fn: 'TItem -> NodeRenderFragment) = render ==> html.renderFragment("ItemContent", fn)
    [<CustomOperation("Placeholder")>] member inline _.Placeholder ([<InlineIfLambda>] render: AttrRenderFragment, fn: Microsoft.AspNetCore.Components.Web.Virtualization.PlaceholderContext -> NodeRenderFragment) = render ==> html.renderFragment("Placeholder", fn)
    [<CustomOperation("ItemSize")>] member inline _.ItemSize ([<InlineIfLambda>] render: AttrRenderFragment, x: System.Single) = render ==> ("ItemSize" => x)
    [<CustomOperation("ItemsProvider")>] member inline _.ItemsProvider ([<InlineIfLambda>] render: AttrRenderFragment, x: Microsoft.AspNetCore.Components.Web.Virtualization.ItemsProviderDelegate<'TItem>) = render ==> ("ItemsProvider" => x)
    [<CustomOperation("Items")>] member inline _.Items ([<InlineIfLambda>] render: AttrRenderFragment, x: System.Collections.Generic.ICollection<'TItem>) = render ==> ("Items" => x)
    [<CustomOperation("OverscanCount")>] member inline _.OverscanCount ([<InlineIfLambda>] render: AttrRenderFragment, x: System.Int32) = render ==> ("OverscanCount" => x)
    [<CustomOperation("SpacerElement")>] member inline _.SpacerElement ([<InlineIfLambda>] render: AttrRenderFragment, x: System.String) = render ==> ("SpacerElement" => x)

            
namespace rec Microsoft.AspNetCore.Components.DslInternals.Forms

open Fun.Blazor
open Fun.Blazor.Operators

type EditFormBuilder<'FunBlazorGeneric when 'FunBlazorGeneric :> Microsoft.AspNetCore.Components.IComponent>() =
    inherit ComponentWithDomAndChildAttrBuilder<'FunBlazorGeneric>()
    [<CustomOperation("AdditionalAttributes")>] member inline _.AdditionalAttributes ([<InlineIfLambda>] render: AttrRenderFragment, x: System.Collections.Generic.IReadOnlyDictionary<System.String, System.Object>) = render ==> ("AdditionalAttributes" => x)
    [<CustomOperation("EditContext")>] member inline _.EditContext ([<InlineIfLambda>] render: AttrRenderFragment, x: Microsoft.AspNetCore.Components.Forms.EditContext) = render ==> ("EditContext" => x)
    [<CustomOperation("Model")>] member inline _.Model ([<InlineIfLambda>] render: AttrRenderFragment, x: System.Object) = render ==> ("Model" => x)
    [<CustomOperation("ChildContent")>] member inline _.ChildContent ([<InlineIfLambda>] render: AttrRenderFragment, fn: Microsoft.AspNetCore.Components.Forms.EditContext -> NodeRenderFragment) = render ==> html.renderFragment("ChildContent", fn)
    [<CustomOperation("OnSubmit")>] member inline _.OnSubmit ([<InlineIfLambda>] render: AttrRenderFragment, fn) = render ==> html.callback<Microsoft.AspNetCore.Components.Forms.EditContext>("OnSubmit", fn)
    [<CustomOperation("OnSubmit")>] member inline _.OnSubmit ([<InlineIfLambda>] render: AttrRenderFragment, fn) = render ==> html.callbackTask<Microsoft.AspNetCore.Components.Forms.EditContext>("OnSubmit", fn)
    [<CustomOperation("OnValidSubmit")>] member inline _.OnValidSubmit ([<InlineIfLambda>] render: AttrRenderFragment, fn) = render ==> html.callback<Microsoft.AspNetCore.Components.Forms.EditContext>("OnValidSubmit", fn)
    [<CustomOperation("OnValidSubmit")>] member inline _.OnValidSubmit ([<InlineIfLambda>] render: AttrRenderFragment, fn) = render ==> html.callbackTask<Microsoft.AspNetCore.Components.Forms.EditContext>("OnValidSubmit", fn)
    [<CustomOperation("OnInvalidSubmit")>] member inline _.OnInvalidSubmit ([<InlineIfLambda>] render: AttrRenderFragment, fn) = render ==> html.callback<Microsoft.AspNetCore.Components.Forms.EditContext>("OnInvalidSubmit", fn)
    [<CustomOperation("OnInvalidSubmit")>] member inline _.OnInvalidSubmit ([<InlineIfLambda>] render: AttrRenderFragment, fn) = render ==> html.callbackTask<Microsoft.AspNetCore.Components.Forms.EditContext>("OnInvalidSubmit", fn)

type InputBaseBuilder<'FunBlazorGeneric, 'TValue when 'FunBlazorGeneric :> Microsoft.AspNetCore.Components.IComponent>() =
    inherit ComponentWithDomAndChildAttrBuilder<'FunBlazorGeneric>()
    [<CustomOperation("AdditionalAttributes")>] member inline _.AdditionalAttributes ([<InlineIfLambda>] render: AttrRenderFragment, x: System.Collections.Generic.IReadOnlyDictionary<System.String, System.Object>) = render ==> ("AdditionalAttributes" => x)
    [<CustomOperation("Value")>] member inline _.Value ([<InlineIfLambda>] render: AttrRenderFragment, x: 'TValue) = render ==> ("Value" => x)
    [<CustomOperation("Value'")>] member inline _.Value' ([<InlineIfLambda>] render: AttrRenderFragment, valueFn: 'TValue * ('TValue -> unit)) = render ==> html.bind("Value", valueFn)
    [<CustomOperation("ValueChanged")>] member inline _.ValueChanged ([<InlineIfLambda>] render: AttrRenderFragment, fn) = render ==> html.callback<'TValue>("ValueChanged", fn)
    [<CustomOperation("ValueChanged")>] member inline _.ValueChanged ([<InlineIfLambda>] render: AttrRenderFragment, fn) = render ==> html.callbackTask<'TValue>("ValueChanged", fn)
    [<CustomOperation("ValueExpression")>] member inline _.ValueExpression ([<InlineIfLambda>] render: AttrRenderFragment, x: System.Linq.Expressions.Expression<System.Func<'TValue>>) = render ==> ("ValueExpression" => x)
    [<CustomOperation("DisplayName")>] member inline _.DisplayName ([<InlineIfLambda>] render: AttrRenderFragment, x: System.String) = render ==> ("DisplayName" => x)

type InputCheckboxBuilder<'FunBlazorGeneric when 'FunBlazorGeneric :> Microsoft.AspNetCore.Components.IComponent>() =
    inherit InputBaseBuilder<'FunBlazorGeneric, System.Boolean>()


type InputDateBuilder<'FunBlazorGeneric, 'TValue when 'FunBlazorGeneric :> Microsoft.AspNetCore.Components.IComponent>() =
    inherit InputBaseBuilder<'FunBlazorGeneric, 'TValue>()
    [<CustomOperation("Type")>] member inline _.Type ([<InlineIfLambda>] render: AttrRenderFragment, x: Microsoft.AspNetCore.Components.Forms.InputDateType) = render ==> ("Type" => x)
    [<CustomOperation("ParsingErrorMessage")>] member inline _.ParsingErrorMessage ([<InlineIfLambda>] render: AttrRenderFragment, x: System.String) = render ==> ("ParsingErrorMessage" => x)

type InputNumberBuilder<'FunBlazorGeneric, 'TValue when 'FunBlazorGeneric :> Microsoft.AspNetCore.Components.IComponent>() =
    inherit InputBaseBuilder<'FunBlazorGeneric, 'TValue>()
    [<CustomOperation("ParsingErrorMessage")>] member inline _.ParsingErrorMessage ([<InlineIfLambda>] render: AttrRenderFragment, x: System.String) = render ==> ("ParsingErrorMessage" => x)

type InputRadioGroupBuilder<'FunBlazorGeneric, 'TValue when 'FunBlazorGeneric :> Microsoft.AspNetCore.Components.IComponent>() =
    inherit InputBaseBuilder<'FunBlazorGeneric, 'TValue>()
    [<CustomOperation("Name")>] member inline _.Name ([<InlineIfLambda>] render: AttrRenderFragment, x: System.String) = render ==> ("Name" => x)

type InputSelectBuilder<'FunBlazorGeneric, 'TValue when 'FunBlazorGeneric :> Microsoft.AspNetCore.Components.IComponent>() =
    inherit InputBaseBuilder<'FunBlazorGeneric, 'TValue>()


type InputTextBuilder<'FunBlazorGeneric when 'FunBlazorGeneric :> Microsoft.AspNetCore.Components.IComponent>() =
    inherit InputBaseBuilder<'FunBlazorGeneric, System.String>()


type InputTextAreaBuilder<'FunBlazorGeneric when 'FunBlazorGeneric :> Microsoft.AspNetCore.Components.IComponent>() =
    inherit InputBaseBuilder<'FunBlazorGeneric, System.String>()


type InputFileBuilder<'FunBlazorGeneric when 'FunBlazorGeneric :> Microsoft.AspNetCore.Components.IComponent>() =
    inherit ComponentWithDomAndChildAttrBuilder<'FunBlazorGeneric>()
    [<CustomOperation("OnChange")>] member inline _.OnChange ([<InlineIfLambda>] render: AttrRenderFragment, fn) = render ==> html.callback<Microsoft.AspNetCore.Components.Forms.InputFileChangeEventArgs>("OnChange", fn)
    [<CustomOperation("OnChange")>] member inline _.OnChange ([<InlineIfLambda>] render: AttrRenderFragment, fn) = render ==> html.callbackTask<Microsoft.AspNetCore.Components.Forms.InputFileChangeEventArgs>("OnChange", fn)
    [<CustomOperation("AdditionalAttributes")>] member inline _.AdditionalAttributes ([<InlineIfLambda>] render: AttrRenderFragment, x: System.Collections.Generic.IDictionary<System.String, System.Object>) = render ==> ("AdditionalAttributes" => x)

type InputRadioBuilder<'FunBlazorGeneric, 'TValue when 'FunBlazorGeneric :> Microsoft.AspNetCore.Components.IComponent>() =
    inherit ComponentWithDomAndChildAttrBuilder<'FunBlazorGeneric>()
    [<CustomOperation("AdditionalAttributes")>] member inline _.AdditionalAttributes ([<InlineIfLambda>] render: AttrRenderFragment, x: System.Collections.Generic.IReadOnlyDictionary<System.String, System.Object>) = render ==> ("AdditionalAttributes" => x)
    [<CustomOperation("Value")>] member inline _.Value ([<InlineIfLambda>] render: AttrRenderFragment, x: 'TValue) = render ==> ("Value" => x)
    [<CustomOperation("Name")>] member inline _.Name ([<InlineIfLambda>] render: AttrRenderFragment, x: System.String) = render ==> ("Name" => x)

type ValidationMessageBuilder<'FunBlazorGeneric, 'TValue when 'FunBlazorGeneric :> Microsoft.AspNetCore.Components.IComponent>() =
    inherit ComponentWithDomAndChildAttrBuilder<'FunBlazorGeneric>()
    [<CustomOperation("AdditionalAttributes")>] member inline _.AdditionalAttributes ([<InlineIfLambda>] render: AttrRenderFragment, x: System.Collections.Generic.IReadOnlyDictionary<System.String, System.Object>) = render ==> ("AdditionalAttributes" => x)
    [<CustomOperation("For'")>] member inline _.For' ([<InlineIfLambda>] render: AttrRenderFragment, x: System.Linq.Expressions.Expression<System.Func<'TValue>>) = render ==> ("For" => x)

type ValidationSummaryBuilder<'FunBlazorGeneric when 'FunBlazorGeneric :> Microsoft.AspNetCore.Components.IComponent>() =
    inherit ComponentWithDomAndChildAttrBuilder<'FunBlazorGeneric>()
    [<CustomOperation("Model")>] member inline _.Model ([<InlineIfLambda>] render: AttrRenderFragment, x: System.Object) = render ==> ("Model" => x)
    [<CustomOperation("AdditionalAttributes")>] member inline _.AdditionalAttributes ([<InlineIfLambda>] render: AttrRenderFragment, x: System.Collections.Generic.IReadOnlyDictionary<System.String, System.Object>) = render ==> ("AdditionalAttributes" => x)

            

// =======================================================================================================================

namespace Microsoft.AspNetCore.Components

[<AutoOpen>]
module DslCE =
  
    open System.Diagnostics.CodeAnalysis
    open Microsoft.AspNetCore.Components.DslInternals

    type ErrorBoundaryBase' [<DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof<ErrorBoundaryBase>)>] () = inherit ErrorBoundaryBaseBuilder<ErrorBoundaryBase>()
            
namespace Microsoft.AspNetCore.Components.Web

[<AutoOpen>]
module DslCE =
  
    open System.Diagnostics.CodeAnalysis
    open Microsoft.AspNetCore.Components.DslInternals.Web

    type ErrorBoundary' [<DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof<ErrorBoundary>)>] () = inherit ErrorBoundaryBuilder<ErrorBoundary>()
    type HeadContent' [<DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof<HeadContent>)>] () = inherit HeadContentBuilder<HeadContent>()
    type HeadOutlet' [<DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof<HeadOutlet>)>] () = inherit HeadOutletBuilder<HeadOutlet>()
    type PageTitle' [<DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof<PageTitle>)>] () = inherit PageTitleBuilder<PageTitle>()
            
namespace Microsoft.AspNetCore.Components.Routing

[<AutoOpen>]
module DslCE =
  
    open System.Diagnostics.CodeAnalysis
    open Microsoft.AspNetCore.Components.DslInternals.Routing

    type FocusOnNavigate' [<DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof<FocusOnNavigate>)>] () = inherit FocusOnNavigateBuilder<FocusOnNavigate>()
    type NavLink' [<DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof<NavLink>)>] () = inherit NavLinkBuilder<NavLink>()
            
namespace Microsoft.AspNetCore.Components.Web.Virtualization

[<AutoOpen>]
module DslCE =
  
    open System.Diagnostics.CodeAnalysis
    open Microsoft.AspNetCore.Components.DslInternals.Web.Virtualization

    type Virtualize'<'TItem> [<DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof<Virtualize<_>>)>] () = inherit VirtualizeBuilder<Virtualize<'TItem>, 'TItem>()
            
namespace Microsoft.AspNetCore.Components.Forms

[<AutoOpen>]
module DslCE =
  
    open System.Diagnostics.CodeAnalysis
    open Microsoft.AspNetCore.Components.DslInternals.Forms

    type EditForm' [<DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof<EditForm>)>] () = inherit EditFormBuilder<EditForm>()
    type InputBase'<'TValue> [<DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof<InputBase<_>>)>] () = inherit InputBaseBuilder<InputBase<'TValue>, 'TValue>()
    type InputCheckbox' [<DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof<InputCheckbox>)>] () = inherit InputCheckboxBuilder<InputCheckbox>()
    type InputDate'<'TValue> [<DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof<InputDate<_>>)>] () = inherit InputDateBuilder<InputDate<'TValue>, 'TValue>()
    type InputNumber'<'TValue> [<DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof<InputNumber<_>>)>] () = inherit InputNumberBuilder<InputNumber<'TValue>, 'TValue>()
    type InputRadioGroup'<'TValue> [<DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof<InputRadioGroup<_>>)>] () = inherit InputRadioGroupBuilder<InputRadioGroup<'TValue>, 'TValue>()
    type InputSelect'<'TValue> [<DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof<InputSelect<_>>)>] () = inherit InputSelectBuilder<InputSelect<'TValue>, 'TValue>()
    type InputText' [<DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof<InputText>)>] () = inherit InputTextBuilder<InputText>()
    type InputTextArea' [<DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof<InputTextArea>)>] () = inherit InputTextAreaBuilder<InputTextArea>()
    type InputFile' [<DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof<InputFile>)>] () = inherit InputFileBuilder<InputFile>()
    type InputRadio'<'TValue> [<DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof<InputRadio<_>>)>] () = inherit InputRadioBuilder<InputRadio<'TValue>, 'TValue>()
    type ValidationMessage'<'TValue> [<DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof<ValidationMessage<_>>)>] () = inherit ValidationMessageBuilder<ValidationMessage<'TValue>, 'TValue>()
    type ValidationSummary' [<DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof<ValidationSummary>)>] () = inherit ValidationSummaryBuilder<ValidationSummary>()